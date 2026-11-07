using System.Text;
using System.Text.Json;
using DiscImageStudio.Localization;
using DiscImageStudio.Core;
using DvdImageSolver.Encoding;
using DvdImageSolver.Solver;

namespace DvdImageSolver;

internal static class Program
{
    private static int Main(string[] args) => RunCommand(args, initializeLanguage: true);

    internal static int RunCommand(string[] args, bool initializeLanguage = false)
    {
        try
        {
            if (initializeLanguage)
            {
                Console.OutputEncoding = new UTF8Encoding(false);
                args = CommandLineLanguage.Initialize(args);
            }
            if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
            {
                PrintHelp();
                return 0;
            }

            return args[0].ToLowerInvariant() switch
            {
                "solve" => Solve(CliArguments.Parse(args.AsSpan(1))),
                "encode" => Encode(CliArguments.Parse(args.AsSpan(1))),
                "calibrate" => Calibrate(CliArguments.Parse(args.AsSpan(1))),
                "simulate" => Simulate(CliArguments.Parse(args.AsSpan(1))),
                "selftest" => RunSelfTests(),
                _ => throw new ArgumentException(L.F($"Unknown command '{args[0]}'.")),
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(L.Format("Cli.Error", exception.Message));
            return 1;
        }
    }

    private static int Solve(CliArguments arguments)
    {
        _ = ReadFilesAtOuterRing(arguments);
        uint fillSectors = arguments.GetUInt("fill-sectors", DvdEccBlockEncoder.SectorCount);
        if (arguments.Has("data-dir")
            || fillSectors != DvdEccBlockEncoder.SectorCount
            || ReadBoolean(arguments, "fast-output", fallback: false))
        {
            return SolveMultipleBlocks(arguments, fillSectors);
        }

        if (ReadBoolean(arguments, "read-compatibility", fallback: false))
            throw new ArgumentException(L.T("--read-compatibility true requires --fast-output true and --algorithm dispersion."));

        string? outputPath = arguments.Has("output") ? arguments.Require("output") : null;
        string? isoOutputPath = arguments.Has("iso-output") ? arguments.Require("iso-output") : null;
        if (outputPath is null && isoOutputPath is null)
        {
            throw new ArgumentException(L.T("Specify --output, --iso-output, or both."));
        }

        if (outputPath is not null
            && isoOutputPath is not null
            && string.Equals(
                Path.GetFullPath(outputPath),
                Path.GetFullPath(isoOutputPath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(L.T("--output and --iso-output must be different files."));
        }

        uint lba = arguments.GetUInt("lba", 0);
        uint psnOffset = arguments.GetUInt("psn-offset", 0x30000);
        int randomSeed = arguments.GetInt("seed", 1);
        ModulationBoundary initial = ReadBoundary(arguments);
        ImageConstraint constraint = ReadConstraint(arguments, lba);
        byte[] initialPayload = LoadOrCreatePayload(arguments, randomSeed);
        SolverAlgorithm algorithm = ReadSolverAlgorithm(arguments);
        ValidateRetiredControlOptions(arguments, algorithm);
        SolverOptions options = new(
            arguments.GetInt("iterations", algorithm switch
            {
                SolverAlgorithm.StateControl => 3,
                SolverAlgorithm.Dispersion => 1,
                _ => 1000,
            }),
            arguments.GetInt("mutation-bytes", 4),
            randomSeed,
            arguments.GetDouble("initial-temperature", 64.0),
            arguments.GetDouble("final-temperature", 0.25),
            algorithm,
            arguments.GetInt("bridge-bytes", 2),
            arguments.GetInt("beam-width", 8),
            arguments.GetInt("retry-beam-width", 32),
            arguments.GetDouble("retry-frame-fraction", 0.25),
            arguments.GetInt("batch-frames", 8),
            arguments.GetInt("batch-candidates", 4),
            arguments.GetInt("search-lanes", 1));

        Console.WriteLine(
            L.Format("ECC Block: LBA {0}, PSN 0x{1:X6}, constraints {2}, algorithm={3}, iterations {4}", lba, checked(lba + psnOffset), constraint.ConstrainedBits, AlgorithmName(algorithm), options.Iterations));
        if (algorithm == SolverAlgorithm.StateControl)
        {
            string adaptive = options.AdaptiveRetryEnabled
                ? L.F($"beam {options.RetryBeamWidth} on failed single frames")
                : L.T("disabled");
            Console.WriteLine(
                L.Format("bridge bytes/edge={0}, beam width={1}, frame fraction/pass={2:P0}, singleton retry={3}, ECC-closed batch={4} frames/{5} candidates, search lanes={6}, payload candidates=state/level/DSV binary byte pairs, scoring=majority error then gray error", options.BridgeBytes, options.BeamWidth, options.RetryFrameFraction, adaptive, options.BatchFrames, options.BatchCandidates, options.SearchLanes));
        }
        else if (algorithm == SolverAlgorithm.Dispersion)
        {
            Console.WriteLine(
                L.Format("{0}, image sampled at every payload code-word center", DispersionPaletteDescription()));
        }
        if (constraint.ImageMapping is { } mapping)
        {
            Console.WriteLine(
                L.Format("image: {0}x{1}, pitch={2:F4} um, block radius={3:F6}..{4:F6} mm", mapping.ImageWidth, mapping.ImageHeight, mapping.TrackPitchMicrometres, mapping.BlockStartRadiusMm, mapping.BlockEndRadiusMm));
        }

        if (arguments.Has("constraint-output"))
        {
            string constraintPath = arguments.Require("constraint-output");
            EnsureParentDirectory(constraintPath);
            constraint.SaveText(constraintPath);
        }

        int progressEvery = Math.Max(1, options.Iterations / 20);
        SolverResult result = SolverEngine.Solve(
            constraint,
            initialPayload,
            lba,
            psnOffset,
            initial,
            options,
            (iteration, current, best) =>
            {
                if (iteration == 1 || iteration % progressEvery == 0 || best == 0)
                {
                    Console.WriteLine(L.Format("iteration {0}: current={1}, best={2}", iteration, current, best));
                }
            });

        if (outputPath is not null)
        {
            EnsureParentDirectory(outputPath);
            File.WriteAllBytes(outputPath, result.Payloads);
        }

        if (arguments.Has("channel-output"))
        {
            string channelPath = arguments.Require("channel-output");
            EnsureParentDirectory(channelPath);
            EncodedEccBlock channelEncoded = result.Encoded.ChannelLevels.Length == 0
                ? new DvdEccBlockEncoder().Encode(result.Payloads, lba, psnOffset, initial)
                : result.Encoded;
            File.WriteAllBytes(channelPath, BitPacking.PackMsbFirst(channelEncoded.ChannelLevels));
        }

        IsoWriteSummary? iso = WriteIso(arguments, isoOutputPath, result.Payloads, lba, constraint.ImageMapping);
        if (outputPath is not null)
        {
            WriteMetadata(
                outputPath + ".json",
                lba,
                psnOffset,
                initial,
                result.Encoded.FinalBoundary,
                result.Score,
                result.ScoreMaximum,
                result.ScoreMetric,
                result.GrayScore,
                result.GrayScoreMaximum,
                result.GrayScoreMetric,
                result.ControlledScore,
                result.ControlledScoreMaximum,
                result.ControlledScoreMetric,
                result.ControlledGrayScore,
                result.ControlledGrayScoreMaximum,
                result.ControlledGrayScoreMetric,
                result.CompletedIterations,
                result.Elapsed,
                randomSeed,
                options,
                constraint.ImageMapping,
                iso);
        }

        if (isoOutputPath is not null)
        {
            WriteMetadata(
                isoOutputPath + ".json",
                lba,
                psnOffset,
                initial,
                result.Encoded.FinalBoundary,
                result.Score,
                result.ScoreMaximum,
                result.ScoreMetric,
                result.GrayScore,
                result.GrayScoreMaximum,
                result.GrayScoreMetric,
                result.ControlledScore,
                result.ControlledScoreMaximum,
                result.ControlledScoreMetric,
                result.ControlledGrayScore,
                result.ControlledGrayScoreMaximum,
                result.ControlledGrayScoreMetric,
                result.CompletedIterations,
                result.Elapsed,
                randomSeed,
                options,
                constraint.ImageMapping,
                iso);
        }

        string outputs = string.Join(
            ", ",
            new[] { outputPath, isoOutputPath }.Where(path => path is not null));
        string gray = result.GrayScore is { } grayScore
            && result.GrayScoreMaximum is { } grayMaximum
            ? $", {result.GrayScoreMetric ?? "gray-error"}={grayScore}/{grayMaximum}"
            : string.Empty;
        string controlled = result.ControlledScore is { } controlledScore
            && result.ControlledScoreMaximum is { } controlledMaximum
            ? $", controlled-majority-error={controlledScore}/{controlledMaximum}"
                + (result.ControlledGrayScore is { } controlledGrayScore
                    && result.ControlledGrayScoreMaximum is { } controlledGrayMaximum
                    ? $", controlled-gray-error={controlledGrayScore}/{controlledGrayMaximum}"
                    : string.Empty)
            : string.Empty;
        Console.WriteLine(
            L.Format("done: {0}={1}/{2}{3}{4}, elapsed={5:F2}s, output={6}", result.ScoreMetric, result.Score, result.ScoreMaximum, gray, controlled, result.Elapsed.TotalSeconds, outputs));
        return 0;
    }

    private static int Encode(CliArguments arguments)
    {
        string inputPath = arguments.Require("input");
        string outputPath = arguments.Require("output");
        byte[] payloads = File.ReadAllBytes(inputPath);
        uint lba = arguments.GetUInt("lba", 0);
        uint psnOffset = arguments.GetUInt("psn-offset", 0x30000);
        ModulationBoundary initial = ReadBoundary(arguments);
        DvdEccBlockEncoder encoder = new();
        EncodedEccBlock result = encoder.Encode(payloads, lba, psnOffset, initial);
        EnsureParentDirectory(outputPath);
        File.WriteAllBytes(outputPath, BitPacking.PackMsbFirst(result.ChannelLevels));
        WriteMetadata(
            outputPath + ".json",
            lba,
            psnOffset,
            initial,
            result.FinalBoundary,
            score: null,
            scoreMaximum: null,
            scoreMetric: null,
            grayScore: null,
            grayScoreMaximum: null,
            grayScoreMetric: null,
            controlledScore: null,
            controlledScoreMaximum: null,
            controlledScoreMetric: null,
            controlledGrayScore: null,
            controlledGrayScoreMaximum: null,
            controlledGrayScoreMetric: null,
            iterations: null,
            elapsed: null,
            randomSeed: null,
            solverOptions: null,
            imageMapping: null,
            iso: null);
        Console.WriteLine(
            L.Format("encoded {0} payload bytes into {1} channel levels ({2} packed bytes)", payloads.Length, result.ChannelLevels.Length, new FileInfo(outputPath).Length));
        return 0;
    }

    private static int Calibrate(CliArguments arguments)
    {
        string imagePath = arguments.Require("image");
        string outputPath = arguments.Require("output");
        int threshold = ReadByteRange(arguments, "image-threshold", 128, minimum: 0);
        int alphaThreshold = ReadByteRange(arguments, "alpha-threshold", 1, minimum: 1);
        uint totalSectors = ReadTotalSectors(arguments);
        uint startLba = arguments.GetUInt("lba", 0);
        uint fillSectors = arguments.Has("fill-sectors")
            ? arguments.GetUInt("fill-sectors", 0)
            : checked(totalSectors - startLba);
        CalibrationPreviewOptions options = new(
            totalSectors,
            startLba,
            fillSectors,
            RequireDouble(arguments, "generated-inner-radius-mm"),
            RequireDouble(arguments, "generated-outer-radius-mm"),
            RequireDouble(arguments, "actual-inner-radius-mm"),
            RequireDouble(arguments, "actual-outer-radius-mm"),
            arguments.GetDouble("channel-bit-nm", 133.3),
            ReadClockwise(arguments),
            checked((byte)threshold),
            checked((byte)alphaThreshold),
            arguments.GetInt("preview-size", 1024),
            arguments.GetInt("samples-per-sector", 16),
            ReadGeneratedPitch(arguments, "linear"),
            ReadGeneratedPitch(arguments, "quadratic"),
            ReadGeneratedPitch(arguments, "cubic"),
            arguments.GetDouble("actual-pitch-linear", 0),
            arguments.GetDouble("actual-pitch-quadratic", 0),
            arguments.GetDouble("actual-pitch-cubic", 0),
            ReadToneProfile(arguments));
        CalibrationPreviewSummary summary = CalibrationPreviewRenderer.Render(imagePath, outputPath, options);
        WriteJson(outputPath + ".json", summary);
        Console.WriteLine(
            L.Format("preview: {0}x{1}, generated pitch={2:F4} um, actual pitch={3:F4} um, output={4}", summary.PreviewSize, summary.PreviewSize, summary.GeneratedTrackPitchMicrometres, summary.ActualTrackPitchMicrometres, outputPath));
        return 0;
    }

    private static int Simulate(CliArguments arguments)
    {
        string isoPath = arguments.Require("iso");
        string outputPath = arguments.Require("output");
        (uint sidecarStartLba, uint sidecarFillSectors, uint sidecarTotalSectors) =
            IsoReadbackRenderer.ReadSolveSummary(isoPath + ".json");
        long isoSectors = new FileInfo(isoPath).Length / DvdEccBlockEncoder.PayloadBytesPerSector;
        uint totalSectors;
        if (arguments.Has("total-sectors") || arguments.Has("total-blocks"))
        {
            totalSectors = ReadTotalSectors(arguments);
        }
        else if (sidecarTotalSectors != 0)
        {
            totalSectors = sidecarTotalSectors;
        }
        else
        {
            totalSectors = checked((uint)isoSectors);
        }

        uint startLba = arguments.GetUInt("lba", sidecarStartLba);
        uint fillSectors;
        if (arguments.Has("fill-sectors"))
        {
            fillSectors = arguments.GetUInt("fill-sectors", 0);
        }
        else if (sidecarFillSectors != 0)
        {
            fillSectors = sidecarFillSectors;
        }
        else
        {
            fillSectors = checked((uint)Math.Min(isoSectors, totalSectors) - startLba);
        }

        IsoReadbackOptions options = new(
            totalSectors,
            startLba,
            fillSectors,
            arguments.GetUInt("psn-offset", 0x30000),
            RequireDouble(arguments, "inner-radius-mm"),
            RequireDouble(arguments, "outer-radius-mm"),
            arguments.GetDouble("channel-bit-nm", 133.3),
            ReadClockwise(arguments),
            arguments.GetInt("preview-size", 1024),
            arguments.GetInt("samples-per-sector", 16),
            arguments.GetDouble("pitch-linear", 0),
            arguments.GetDouble("pitch-quadratic", 0),
            arguments.GetDouble("pitch-cubic", 0));
        IsoReadbackSummary summary = IsoReadbackRenderer.Render(isoPath, outputPath, options);
        WriteJson(outputPath + ".json", summary);
        Console.WriteLine(
            L.Format("readback: {0}x{1}, classified={2}/{3}, pitch={4:F4} um, output={5}", summary.PreviewSize, summary.PreviewSize, summary.ClassifiedSamples, summary.TotalSamples, summary.TrackPitchMicrometres, outputPath));
        return 0;
    }

    private static int SolveMultipleBlocks(CliArguments arguments, uint fillSectors)
    {
        bool hybridMode = arguments.Has("data-dir");
        bool filesAtOuterRing = ReadFilesAtOuterRing(arguments);
        if (!arguments.Has("image") || arguments.Has("target"))
        {
            throw new ArgumentException(L.T("Range or fast output requires --image and does not accept --target."));
        }

        string isoOutputPath = arguments.Require("iso-output");
        string[] unsupported =
        [
            "output",
            "iso-template",
            "iso-sectors",
            "seed-payload",
            "channel-output",
            "constraint-output",
        ];
        string? unsupportedOption = unsupported.FirstOrDefault(arguments.Has);
        if (unsupportedOption is not null)
        {
            throw new ArgumentException(L.F($"--{unsupportedOption} is not supported with multi-block --fill-sectors."));
        }

        if (hybridMode && (arguments.Has("lba") || arguments.Has("fill-sectors")))
        {
            throw new ArgumentException(
                L.T("Hybrid --data-dir mode calculates --lba and --fill-sectors automatically; use --drawing-start-lba only when extra inner-ring space is required."));
        }

        if (!hybridMode && arguments.Has("drawing-start-lba"))
        {
            throw new ArgumentException(L.T("--drawing-start-lba requires --data-dir."));
        }

        uint psnOffset = arguments.GetUInt("psn-offset", 0x30000);
        int randomSeed = arguments.GetInt("seed", 1);
        string initialMode = arguments.Get("initial", "random");
        bool zeroInitial = initialMode.Equals("zero", StringComparison.OrdinalIgnoreCase)
            ? true
            : initialMode.Equals("random", StringComparison.OrdinalIgnoreCase)
                ? false
                : throw new ArgumentException(L.T("--initial must be 'random' or 'zero'."));
        SolverAlgorithm algorithm = ReadSolverAlgorithm(
            arguments,
            hybridMode ? "dispersion" : "full");
        bool fastOutput = ReadBoolean(arguments, "fast-output", fallback: hybridMode);
        if (fastOutput && algorithm != SolverAlgorithm.Dispersion)
        {
            throw new ArgumentException(L.T("--fast-output true requires --algorithm dispersion."));
        }

        bool readCompatibility = ReadBoolean(arguments, "read-compatibility", fallback: false);
        if (readCompatibility && !fastOutput)
            throw new ArgumentException(L.T("--read-compatibility true requires --fast-output true and --algorithm dispersion."));

        int iterationsPerBlock = arguments.Has("iterations-per-block")
            ? arguments.GetInt("iterations-per-block", 0)
            : arguments.GetInt(
                "iterations",
                algorithm switch
                {
                    SolverAlgorithm.StateControl => 3,
                    SolverAlgorithm.Dispersion => 1,
                    _ => 100,
                });
        ValidateRetiredControlOptions(arguments, algorithm);
        ImageMappingOptions imageMapping = ReadImageMappingOptions(arguments, defaultConstraintStep: 64);
        ModulationBoundary discInitialBoundary = ReadBoundary(arguments);
        ModulationBoundary drawingInitialBoundary = discInitialBoundary;
        HybridIsoWriteSummary? hybridIso = null;
        uint lba;
        if (hybridMode)
        {
            if ((psnOffset & 0xF) != 0)
            {
                throw new ArgumentException(
                    L.T("Hybrid mode requires --psn-offset to be aligned to a 16-sector ECC Block."));
            }

            HybridIsoImageWriter.LayoutPlan isoLayout = HybridIsoImageWriter.Plan(
                arguments.Require("data-dir"),
                imageMapping.TotalSectors,
                arguments.Get("volume-label", "DVD_IMAGE"),
                filesAtOuterRing, arguments.Get("file-system", "iso9660").ToLowerInvariant() switch
                {
                    "iso9660" => false,
                    "udf250" => true,
                    _ => throw new ArgumentException(L.T("--file-system must be iso9660 or udf250.")),
                });
            uint minimumDrawingLba = AlignToEccBlock(isoLayout.DrawingStartLba, psnOffset);
            lba = arguments.Has("drawing-start-lba")
                ? arguments.GetUInt("drawing-start-lba", 0)
                : minimumDrawingLba;
            if (lba < minimumDrawingLba)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(arguments),
                    L.F($"--drawing-start-lba must be at least {minimumDrawingLba} so it does not overwrite files."));
            }

            if (((lba + psnOffset) & 0xF) != 0)
            {
                throw new ArgumentException(
                    L.T("--drawing-start-lba plus --psn-offset must be aligned to 16 sectors."));
            }

            if (lba > imageMapping.TotalSectors)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(arguments),
                    L.T("--drawing-start-lba exceeds the reported disc size."));
            }

            uint drawingEndLba = isoLayout.DrawingEndLbaExclusive / DvdEccBlockEncoder.SectorCount * DvdEccBlockEncoder.SectorCount;
            if (drawingEndLba < lba)
                throw new ArgumentException(L.T("The selected drawing range overlaps the outer-ring files."));
            fillSectors = checked((drawingEndLba - lba)
                / DvdEccBlockEncoder.SectorCount
                * DvdEccBlockEncoder.SectorCount);
            if (fillSectors < DvdEccBlockEncoder.SectorCount)
            {
                throw new ArgumentException(
                    L.T("The filesystem leaves no complete ECC Block for drawing in the selected file layout."));
            }

            hybridIso = HybridIsoImageWriter.Create(isoLayout, isoOutputPath);

            Console.WriteLine(
                L.Format("hybrid ISO: files={0}, directories={1}, data={2} bytes, filesystem end LBA={3}, files={4}, drawing LBA {5}..{6}", hybridIso.FileCount, hybridIso.DirectoryCount, hybridIso.FileBytes, hybridIso.DataEndLbaExclusive, (filesAtOuterRing ? "outer" : "inner"), lba, lba + fillSectors - 1));
            if (!fastOutput)
            {
                Console.WriteLine(L.Format("replaying inner-ring modulation boundary: 0..{0}", lba - 1));
                drawingInitialBoundary = ReplayIsoBoundary(
                    isoOutputPath,
                    lba,
                    psnOffset,
                    discInitialBoundary);
                Console.WriteLine(
                    L.Format("drawing boundary: state={0}, DSV={1}, level={2}", drawingInitialBoundary.State, drawingInitialBoundary.Dsv, drawingInitialBoundary.LevelName));
            }
        }
        else
        {
            lba = arguments.GetUInt("lba", 0);
        }

        MultiBlockSolveOptions options = new(
            lba,
            fillSectors,
            psnOffset,
            iterationsPerBlock,
            arguments.GetInt("mutation-bytes", 4),
            randomSeed,
            arguments.GetDouble("initial-temperature", 64.0),
            arguments.GetDouble("final-temperature", 0.25),
            zeroInitial,
            imageMapping,
            algorithm,
            arguments.GetInt("bridge-bytes", 2),
            arguments.GetInt("beam-width", 8),
            arguments.GetInt("retry-beam-width", 32),
            arguments.GetDouble("retry-frame-fraction", 0.25),
            arguments.GetInt("batch-frames", 8),
            arguments.GetInt("batch-candidates", 4),
            arguments.GetInt("search-lanes", 1),
            arguments.GetInt("fast-parallelism", 0),
            PreserveExistingIso: hybridMode,
            ReadCompatibility: readCompatibility);
        int totalBlocks = checked((int)(fillSectors / DvdEccBlockEncoder.SectorCount));
        int progressEvery = Math.Max(1, totalBlocks / 100);
        if (fastOutput)
        {
            return WriteFastDispersionImage(
                arguments,
                isoOutputPath,
                options,
                progressEvery,
                hybridIso);
        }

        Console.WriteLine(
            L.Format("raw ISO fill: LBA {0}..{1}, ECC blocks={2}, iterations/block={3}, algorithm={4}, constraint step={5}", lba, checked(lba + fillSectors - 1), totalBlocks, iterationsPerBlock, AlgorithmName(algorithm), imageMapping.SampleEveryChannelBits));
        if (algorithm == SolverAlgorithm.Dispersion)
        {
            Console.WriteLine(
                DispersionPaletteDescription());
        }
        MultiBlockSolveResult result = MultiBlockImageSolver.Solve(
            arguments.Require("image"),
            isoOutputPath,
            drawingInitialBoundary,
            options,
            progress =>
            {
                if (progress.CompletedBlocks == 1
                    || progress.CompletedBlocks == progress.TotalBlocks
                    || progress.CompletedBlocks % progressEvery == 0)
                {
                    Console.WriteLine(
                        L.Format("block {0}/{1}, LBA={2}, {3}={4}/{5}{6}{7}state={8}, DSV={9}, elapsed={10:F1}s", progress.CompletedBlocks, progress.TotalBlocks, progress.BlockLba, progress.ScoreMetric, progress.Score, progress.ScoreMaximum, (progress.GrayScore is { } grayScore
                            && progress.GrayScoreMaximum is { } grayMaximum
                            ? $", {progress.GrayScoreMetric ?? "gray-error"}={grayScore}/{grayMaximum}, "
                            : ", "), (progress.ControlledScore is { } controlledScore
                            && progress.ControlledScoreMaximum is { } controlledMaximum
                            ? $"controlled-majority={controlledScore}/{controlledMaximum}"
                                + (progress.ControlledGrayScore is { } controlledGrayScore
                                    && progress.ControlledGrayScoreMaximum is { } controlledGrayMaximum
                                    ? $", controlled-gray={controlledGrayScore}/{controlledGrayMaximum}, "
                                    : ", ")
                            : string.Empty), progress.FinalBoundary.State, progress.FinalBoundary.Dsv, progress.Elapsed.TotalSeconds));
                }
            });

        var metadata = new
        {
            format = "dvd-nrzi-disc-fill-v2",
            sourceImage = Path.GetFullPath(arguments.Require("image")),
            isoOutput = Path.GetFullPath(isoOutputPath),
            startLba = lba,
            filesAtOuterRing = hybridIso?.FilesAtOuterRing ?? false,
            fillSectors,
            totalSectors = imageMapping.TotalSectors,
            psnOffset = $"0x{psnOffset:X}",
            blockCount = result.CompletedBlocks,
            algorithm = AlgorithmName(algorithm),
            iterationsPerBlock,
            mutationBytes = algorithm == SolverAlgorithm.Full ? (int?)options.MutationBytes : null,
            bridgeBytes = algorithm == SolverAlgorithm.StateControl ? (int?)options.BridgeBytes : null,
            beamWidth = algorithm == SolverAlgorithm.StateControl ? (int?)options.BeamWidth : null,
            retryBeamWidth = algorithm == SolverAlgorithm.StateControl ? (int?)options.RetryBeamWidth : null,
            retryFrameFraction = algorithm == SolverAlgorithm.StateControl
                ? (double?)options.RetryFrameFraction
                : null,
            adaptiveRetryEnabled = algorithm == SolverAlgorithm.StateControl
                ? (bool?)(options.RetryBeamWidth > options.BeamWidth)
                : null,
            batchFrames = algorithm == SolverAlgorithm.StateControl ? (int?)options.BatchFrames : null,
            batchCandidates = algorithm == SolverAlgorithm.StateControl
                ? (int?)options.BatchCandidates
                : null,
            searchLanes = algorithm == SolverAlgorithm.StateControl
                ? (int?)options.SearchLanes
                : null,
            binaryBytePairMode = algorithm == SolverAlgorithm.StateControl
                ? "efm-state-input-level-and-current-dsv"
                : null,
            binaryBytePairs = algorithm == SolverAlgorithm.StateControl
                ? BinaryBytePairMetadata()
                : null,
            dispersionPool = algorithm == SolverAlgorithm.Dispersion
                ? DispersionPaletteMetadata()
                : null,
            constraintStep = imageMapping.SampleEveryChannelBits,
            initialBoundary = new
            {
                state = drawingInitialBoundary.State,
                dsv = drawingInitialBoundary.Dsv,
                level = drawingInitialBoundary.LevelName,
            },
            discInitialBoundary = hybridIso is null
                ? null
                : (object)new
                {
                    state = discInitialBoundary.State,
                    dsv = discInitialBoundary.Dsv,
                    level = discInitialBoundary.LevelName,
                },
            finalBoundary = new
            {
                state = result.FinalBoundary.State,
                dsv = result.FinalBoundary.Dsv,
                level = result.FinalBoundary.LevelName,
            },
            result.TotalScore,
            result.TotalScoreMaximum,
            result.ScoreMetric,
            result.TotalGrayScore,
            result.TotalGrayScoreMaximum,
            result.GrayScoreMetric,
            result.TotalControlledScore,
            result.TotalControlledScoreMaximum,
            result.TotalControlledGrayScore,
            result.TotalControlledGrayScoreMaximum,
            normalizedScore = result.TotalScoreMaximum == 0
                ? 0
                : (double)result.TotalScore / result.TotalScoreMaximum,
            normalizedGrayScore = result.TotalGrayScore is null
                || result.TotalGrayScoreMaximum is null
                || result.TotalGrayScoreMaximum == 0
                    ? null
                    : (double?)result.TotalGrayScore / result.TotalGrayScoreMaximum,
            normalizedControlledScore = result.TotalControlledScore is null
                || result.TotalControlledScoreMaximum is null
                || result.TotalControlledScoreMaximum == 0
                    ? null
                    : (double?)result.TotalControlledScore / result.TotalControlledScoreMaximum,
            normalizedControlledGrayScore = result.TotalControlledGrayScore is null
                || result.TotalControlledGrayScoreMaximum is null
                || result.TotalControlledGrayScoreMaximum == 0
                    ? null
                    : (double?)result.TotalControlledGrayScore
                        / result.TotalControlledGrayScoreMaximum,
            elapsedSeconds = result.Elapsed.TotalSeconds,
            imageMapping,
            pitchPolynomial = DescribePitchPolynomial(imageMapping),
            iso = new
            {
                mode = hybridIso?.Mode ?? "raw-sector-image",
                logicalSectorBytes = IsoImageWriter.LogicalSectorBytes,
                imageSectors = result.IsoSectors,
                sparseFile = result.SparseIso,
                mountableFilesystem = hybridIso is not null,
                hybrid = hybridIso,
            },
        };
        WriteJson(isoOutputPath + ".json", metadata);
        Console.WriteLine(
            L.Format("done: blocks={0}, {1}={2}/{3}{4}{5}elapsed={6:F1}s, output={7}", result.CompletedBlocks, result.ScoreMetric, result.TotalScore, result.TotalScoreMaximum, (result.TotalGrayScore is { } grayScore
                && result.TotalGrayScoreMaximum is { } grayMaximum
                ? $", {result.GrayScoreMetric ?? "gray-error"}={grayScore}/{grayMaximum}, "
                : ", "), (result.TotalControlledScore is { } controlledScore
                && result.TotalControlledScoreMaximum is { } controlledMaximum
                ? $"controlled-majority={controlledScore}/{controlledMaximum}"
                    + (result.TotalControlledGrayScore is { } controlledGrayScore
                        && result.TotalControlledGrayScoreMaximum is { } controlledGrayMaximum
                        ? $", controlled-gray={controlledGrayScore}/{controlledGrayMaximum}, "
                        : ", ")
                : string.Empty), result.Elapsed.TotalSeconds, isoOutputPath));
        return 0;
    }

    private static bool ReadFilesAtOuterRing(CliArguments arguments)
    {
        bool outer = arguments.Get("file-position", "inner") switch
        {
            "inner" => false,
            "outer" => true,
            _ => throw new ArgumentException(L.T("--file-position must be 'inner' or 'outer'.")),
        };
        if (outer && !arguments.Has("data-dir"))
            throw new ArgumentException(L.T("--file-position outer requires --data-dir."));
        return outer;
    }

    private static int WriteFastDispersionImage(
        CliArguments arguments,
        string isoOutputPath,
        MultiBlockSolveOptions options,
        int progressEvery,
        HybridIsoWriteSummary? hybridIso)
    {
        Console.WriteLine(
            L.Format("fast raw ISO fill: LBA {0}..{1}, ECC blocks={2}, algorithm=dispersion, ECC/EFM replay=skipped, workers={3}", options.StartLba, checked(options.StartLba + options.FillSectors - 1), options.FillSectors / DvdEccBlockEncoder.SectorCount, (options.FastParallelism == 0 ? Environment.ProcessorCount : options.FastParallelism)));
        Console.WriteLine(
            L.Format("{0}, image sampled at every payload code-word center", DispersionPaletteDescription()));

        if (options.ReadCompatibility)
            Console.WriteLine(L.Format("read compatibility={0} (experimental drawing-byte protection)", options.ReadCompatibilityMode));

        FastDispersionWriteResult result = FastDvdImageFileWriter.WritePrepared(
            arguments.Require("image"),
            isoOutputPath,
            options,
            hybridIso,
            progress =>
            {
                if (progress.CompletedBlocks == 1
                    || progress.CompletedBlocks == progress.TotalBlocks
                    || progress.CompletedBlocks % progressEvery == 0)
                {
                    double mebibytes = progress.CompletedBlocks
                        * (double)DvdEccBlockEncoder.PayloadBytesPerBlock
                        / (1024 * 1024);
                    double rate = progress.Elapsed.TotalSeconds <= 0
                        ? 0
                        : mebibytes / progress.Elapsed.TotalSeconds;
                    Console.WriteLine(
                        L.Format("block {0}/{1}, LBA={2}, controlled={3}, rate={4:F2} MiB/s, elapsed={5:F1}s", progress.CompletedBlocks, progress.TotalBlocks, progress.BlockLba, progress.ControlledWords, rate, progress.Elapsed.TotalSeconds));
                }
            });

        double totalMebibytes = result.FilledSectors
            * (double)IsoImageWriter.LogicalSectorBytes
            / (1024 * 1024);
        double finalRate = result.Elapsed.TotalSeconds <= 0
            ? 0
            : totalMebibytes / result.Elapsed.TotalSeconds;
        Console.WriteLine(
            $"done: blocks={result.CompletedBlocks}, controlled={result.ControlledWords}, "
            + (options.ReadCompatibility
                ? $"compatibility changes={result.CompatibilityChanges}, payload-transition-class-error=not evaluated, "
                : "payload-transition-class-error=0, ")
            + "boundary=not replayed, "
            + $"rate={finalRate:F2} MiB/s, elapsed={result.Elapsed.TotalSeconds:F1}s, "
            + $"output={isoOutputPath}");
        return 0;
    }

    private static byte[] LoadOrCreatePayload(CliArguments arguments, int randomSeed)
    {
        if (arguments.Has("seed-payload"))
        {
            byte[] payload = File.ReadAllBytes(arguments.Require("seed-payload"));
            if (payload.Length != DvdEccBlockEncoder.PayloadBytesPerBlock)
            {
                throw new ArgumentException(
                    L.F($"Seed payload must contain exactly {DvdEccBlockEncoder.PayloadBytesPerBlock} bytes."));
            }

            return payload;
        }

        byte[] generated = new byte[DvdEccBlockEncoder.PayloadBytesPerBlock];
        string mode = arguments.Get("initial", "random");
        if (mode.Equals("random", StringComparison.OrdinalIgnoreCase))
        {
            new Random(randomSeed).NextBytes(generated);
        }
        else if (!mode.Equals("zero", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(L.T("--initial must be 'random' or 'zero'."));
        }

        return generated;
    }

    private static ImageConstraint ReadConstraint(CliArguments arguments, uint initialLba)
    {
        bool hasTarget = arguments.Has("target");
        bool hasImage = arguments.Has("image");
        if (hasTarget == hasImage)
        {
            throw new ArgumentException(L.T("Specify exactly one of --target or --image."));
        }

        if (hasTarget)
        {
            return ImageConstraint.Load(arguments.Require("target"), arguments.GetInt("target-sector", 0));
        }

        ImageMappingOptions options = ReadImageMappingOptions(arguments, defaultConstraintStep: 1);
        return ImageConstraint.LoadImage(arguments.Require("image"), initialLba, options);
    }

    private static ImageMappingOptions ReadImageMappingOptions(
        CliArguments arguments,
        int defaultConstraintStep)
    {
        int threshold = ReadByteRange(arguments, "image-threshold", 128, minimum: 0);
        int alphaThreshold = ReadByteRange(arguments, "alpha-threshold", 1, minimum: 1);
        return new ImageMappingOptions(
            ReadTotalSectors(arguments),
            arguments.GetDouble("inner-radius-mm", 24.0),
            arguments.GetDouble("outer-radius-mm", 58.0),
            arguments.GetDouble("channel-bit-nm", 133.3),
            arguments.GetDouble("start-angle-deg", 0.0),
            ReadClockwise(arguments),
            checked((byte)threshold),
            checked((byte)alphaThreshold),
            arguments.GetInt("constraint-step", defaultConstraintStep),
            arguments.GetDouble("pitch-linear", 0),
            arguments.GetDouble("pitch-quadratic", 0),
            arguments.GetDouble("pitch-cubic", 0),
            ReadToneProfile(arguments));
    }

    /// <summary>Optional tone mapping of the sampled picture. Absent means the historical
    /// hard threshold, so existing command lines produce the same bytes.</summary>
    private static ToneProfile? ReadToneProfile(CliArguments arguments)
    {
        if (!arguments.Has("tone-dither")
            && !arguments.Has("tone-levels")
            && !arguments.Has("tone-brightness")
            && !arguments.Has("tone-gamma"))
        {
            return null;
        }

        ToneProfile tone = new(
            arguments.GetDouble("tone-dither", 0),
            arguments.GetInt("tone-levels", ToneProfile.TwoLevels),
            arguments.GetDouble("tone-brightness", 0),
            arguments.GetBoolean("tone-gamma", false));
        tone.Validate();
        if (!tone.IsDvdCompatible)
        {
            throw new ArgumentException(
                L.T("DVD dispersion uses two textures; --tone-levels must be 2."));
        }

        return tone;
    }

    private static double ReadGeneratedPitch(CliArguments arguments, string order)
    {
        string plain = $"pitch-{order}";
        string generated = $"generated-pitch-{order}";
        if (arguments.Has(plain) && arguments.Has(generated))
            throw new ArgumentException(L.F($"Use only one of --{plain} and --{generated}."));
        return arguments.GetDouble(arguments.Has(generated) ? generated : plain, 0);
    }

    private static object DescribePitchPolynomial(ImageMappingOptions options)
        => FastDvdImageFileWriter.DescribePitchPolynomial(options);

    private static uint ReadTotalSectors(CliArguments arguments)
    {
        bool hasSectors = arguments.Has("total-sectors");
        bool hasBlocks = arguments.Has("total-blocks");
        if (hasSectors && hasBlocks)
        {
            throw new ArgumentException(L.T("Use only one of --total-sectors and --total-blocks."));
        }

        if (!hasSectors && !hasBlocks)
        {
            throw new ArgumentException(L.T("Image mapping requires --total-sectors (or its alias --total-blocks)."));
        }

        return arguments.GetUInt(hasSectors ? "total-sectors" : "total-blocks", 0);
    }

    private static uint AlignToEccBlock(uint minimumLba, uint psnOffset)
    {
        uint remainder = checked((minimumLba + psnOffset) & 0xF);
        return remainder == 0
            ? minimumLba
            : checked(minimumLba + DvdEccBlockEncoder.SectorCount - remainder);
    }

    private static ModulationBoundary ReplayIsoBoundary(
        string isoPath,
        uint sectorCount,
        uint psnOffset,
        ModulationBoundary initialBoundary)
    {
        if (sectorCount % DvdEccBlockEncoder.SectorCount != 0)
        {
            throw new ArgumentException(L.T("Boundary replay sector count must be aligned to 16 sectors."));
        }

        int totalBlocks = checked((int)(sectorCount / DvdEccBlockEncoder.SectorCount));
        int progressEvery = Math.Max(1, totalBlocks / 100);
        byte[] payloads = new byte[DvdEccBlockEncoder.PayloadBytesPerBlock];
        ModulationBoundary boundary = initialBoundary;
        using FileStream iso = new(
            isoPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 128 * 1024,
            FileOptions.SequentialScan);
        for (int blockIndex = 0; blockIndex < totalBlocks; blockIndex++)
        {
            iso.ReadExactly(payloads);
            uint lba = checked((uint)blockIndex * DvdEccBlockEncoder.SectorCount);
            byte[] recordingFrames = DvdEccBlockEncoder.BuildRecordingFrames(
                payloads,
                checked(lba + psnOffset));
            boundary = DvdEccBlockEncoder.ReplayRecordingFrameBoundary(
                recordingFrames,
                boundary);
            int completed = blockIndex + 1;
            if (completed == 1 || completed == totalBlocks || completed % progressEvery == 0)
            {
                Console.WriteLine(
                    L.Format("inner boundary block {0}/{1}, state={2}, DSV={3}, level={4}", completed, totalBlocks, boundary.State, boundary.Dsv, boundary.LevelName));
            }
        }

        return boundary;
    }

    private static IsoWriteSummary? WriteIso(
        CliArguments arguments,
        string? isoOutputPath,
        ReadOnlySpan<byte> payloads,
        uint lba,
        ImageMappingSummary? imageMapping)
    {
        if (isoOutputPath is null)
        {
            if (arguments.Has("iso-template") || arguments.Has("iso-sectors"))
            {
                throw new ArgumentException(L.T("--iso-template and --iso-sectors require --iso-output."));
            }

            return null;
        }

        if (arguments.Has("iso-template"))
        {
            if (arguments.Has("iso-sectors"))
            {
                throw new ArgumentException(L.T("--iso-sectors cannot be combined with --iso-template."));
            }

            string templatePath = arguments.Require("iso-template");
            uint templateSectors = IsoImageWriter.GetTemplateSectorCount(templatePath);
            if (imageMapping is not null && templateSectors != imageMapping.TotalSectors)
            {
                throw new ArgumentException(
                    L.F($"ISO template has {templateSectors} sectors, but image mapping used {imageMapping.TotalSectors} sectors."));
            }

            return IsoImageWriter.PatchTemplate(templatePath, isoOutputPath, payloads, lba);
        }

        uint volumeSectors;
        if (arguments.Has("iso-sectors"))
        {
            volumeSectors = arguments.GetUInt("iso-sectors", 0);
            if (imageMapping is not null && volumeSectors != imageMapping.TotalSectors)
            {
                throw new ArgumentException(
                    L.F($"--iso-sectors is {volumeSectors}, but image mapping used {imageMapping.TotalSectors} sectors."));
            }
        }
        else if (imageMapping is not null)
        {
            volumeSectors = imageMapping.TotalSectors;
        }
        else
        {
            throw new ArgumentException(
                L.T("Minimal ISO output requires --iso-sectors when the constraint does not come from an image."));
        }

        return IsoImageWriter.CreateMinimal(
            isoOutputPath,
            payloads,
            lba,
            volumeSectors,
            arguments.Get("volume-label", "DVD_IMAGE"));
    }

    private static ModulationBoundary ReadBoundary(CliArguments arguments)
    {
        int state = arguments.GetInt("initial-state", 1);
        int dsv = arguments.GetInt("initial-dsv", 0);
        string level = arguments.Get("initial-level", "land");
        bool isLand = level.Equals("land", StringComparison.OrdinalIgnoreCase)
            ? true
            : level.Equals("pit", StringComparison.OrdinalIgnoreCase)
                ? false
                : throw new ArgumentException(L.T("--initial-level must be 'pit' or 'land'."));
        return ModulationBoundary.Validate(checked((byte)state), dsv, isLand);
    }

    private static bool ReadClockwise(CliArguments arguments)
    {
        string direction = arguments.Get("spiral-direction", "cw");
        return direction.Equals("cw", StringComparison.OrdinalIgnoreCase)
            ? true
            : direction.Equals("ccw", StringComparison.OrdinalIgnoreCase)
                ? false
                : throw new ArgumentException(L.T("--spiral-direction must be 'ccw' or 'cw'."));
    }

    private static SolverAlgorithm ReadSolverAlgorithm(
        CliArguments arguments,
        string fallback = "full")
    {
        string value = arguments.Get("algorithm", fallback);
        return value.ToLowerInvariant() switch
        {
            "full" or "exact" => SolverAlgorithm.Full,
            "state-control" or "control" => SolverAlgorithm.StateControl,
            "dispersion" or "byte-pool" => SolverAlgorithm.Dispersion,
            _ => throw new ArgumentException(
                L.T("--algorithm must be 'full' (alias 'exact'), 'state-control' (alias 'control'), or 'dispersion' (alias 'byte-pool').")),
        };
    }

    private static string AlgorithmName(SolverAlgorithm algorithm)
        => algorithm switch
        {
            SolverAlgorithm.StateControl => "state-control",
            SolverAlgorithm.Dispersion => "dispersion",
            _ => "full",
        };

    private static void ValidateRetiredControlOptions(CliArguments arguments, SolverAlgorithm algorithm)
    {
        if (arguments.Has("frame-candidates")
            || arguments.Has("parallel-candidates")
            || arguments.Has("parallelism")
            || arguments.Has("word-candidates"))
        {
            throw new ArgumentException(L.T("--frame-candidates, --parallel-candidates, --parallelism and --word-candidates belonged to removed search modes; state-control now uses state/level/current-DSV binary byte pairs and --beam-width."));
        }

        if (algorithm != SolverAlgorithm.StateControl)
        {
            return;
        }

        if (arguments.Has("control-bytes"))
        {
            throw new ArgumentException(L.T("--control-bytes belonged to the removed control-pool algorithm; use --bridge-bytes."));
        }

        if (arguments.Has("mutation-bytes"))
        {
            throw new ArgumentException(
                L.T("--mutation-bytes is only used by --algorithm full; state-control uses deterministic codeword selection."));
        }
    }

    private static int ReadByteRange(CliArguments arguments, string key, int fallback, int minimum)
    {
        int value = arguments.GetInt(key, fallback);
        if (value < minimum || value > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(arguments), L.F($"--{key} must be {minimum}..255."));
        }

        return value;
    }

    private static bool ReadBoolean(CliArguments arguments, string key, bool fallback)
    {
        if (!arguments.Has(key))
        {
            return fallback;
        }

        string value = arguments.Require(key);
        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value == "1"
                ? true
                : value.Equals("false", StringComparison.OrdinalIgnoreCase)
                    || value.Equals("no", StringComparison.OrdinalIgnoreCase)
                    || value == "0"
                        ? false
                        : throw new ArgumentException(
                            L.F($"--{key} must be true/false, yes/no, or 1/0."));
    }

    private static double RequireDouble(CliArguments arguments, string key)
        => arguments.Has(key)
            ? arguments.GetDouble(key, 0)
            : throw new ArgumentException(L.F($"Missing required option --{key}."));

    private static void WriteMetadata(
        string path,
        uint lba,
        uint psnOffset,
        ModulationBoundary initial,
        ModulationBoundary final,
        int? score,
        int? scoreMaximum,
        string? scoreMetric,
        int? grayScore,
        int? grayScoreMaximum,
        string? grayScoreMetric,
        int? controlledScore,
        int? controlledScoreMaximum,
        string? controlledScoreMetric,
        int? controlledGrayScore,
        int? controlledGrayScoreMaximum,
        string? controlledGrayScoreMetric,
        int? iterations,
        TimeSpan? elapsed,
        int? randomSeed,
        SolverOptions? solverOptions,
        ImageMappingSummary? imageMapping,
        IsoWriteSummary? iso)
    {
        var metadata = new
        {
            format = "dvd-nrzi-image-solver-v2",
            initialLba = lba,
            psnOffset = $"0x{psnOffset:X}",
            firstPsn = $"0x{checked(lba + psnOffset):X6}",
            sectorCount = DvdEccBlockEncoder.SectorCount,
            initialBoundary = new { state = initial.State, dsv = initial.Dsv, level = initial.LevelName },
            finalBoundary = new { state = final.State, dsv = final.Dsv, level = final.LevelName },
            score,
            scoreMaximum,
            scoreMetric,
            normalizedScore = score is null || scoreMaximum is null || scoreMaximum == 0
                ? null
                : (double?)score / scoreMaximum,
            grayScore,
            grayScoreMaximum,
            grayScoreMetric,
            normalizedGrayScore = grayScore is null
                || grayScoreMaximum is null
                || grayScoreMaximum == 0
                    ? null
                    : (double?)grayScore / grayScoreMaximum,
            controlledScore,
            controlledScoreMaximum,
            controlledScoreMetric,
            normalizedControlledScore = controlledScore is null
                || controlledScoreMaximum is null
                || controlledScoreMaximum == 0
                    ? null
                    : (double?)controlledScore / controlledScoreMaximum,
            controlledGrayScore,
            controlledGrayScoreMaximum,
            controlledGrayScoreMetric,
            normalizedControlledGrayScore = controlledGrayScore is null
                || controlledGrayScoreMaximum is null
                || controlledGrayScoreMaximum == 0
                    ? null
                    : (double?)controlledGrayScore / controlledGrayScoreMaximum,
            iterations,
            elapsedSeconds = elapsed?.TotalSeconds,
            randomSeed,
            algorithm = solverOptions is null ? null : AlgorithmName(solverOptions.Algorithm),
            mutationBytes = solverOptions?.Algorithm == SolverAlgorithm.Full ? (int?)solverOptions.MutationBytes : null,
            bridgeBytes = solverOptions?.Algorithm == SolverAlgorithm.StateControl ? (int?)solverOptions.BridgeBytes : null,
            beamWidth = solverOptions?.Algorithm == SolverAlgorithm.StateControl ? (int?)solverOptions.BeamWidth : null,
            retryBeamWidth = solverOptions?.Algorithm == SolverAlgorithm.StateControl
                ? (int?)solverOptions.RetryBeamWidth
                : null,
            retryFrameFraction = solverOptions?.Algorithm == SolverAlgorithm.StateControl
                ? (double?)solverOptions.RetryFrameFraction
                : null,
            adaptiveRetryEnabled = solverOptions?.Algorithm == SolverAlgorithm.StateControl
                ? (bool?)solverOptions.AdaptiveRetryEnabled
                : null,
            batchFrames = solverOptions?.Algorithm == SolverAlgorithm.StateControl
                ? (int?)solverOptions.BatchFrames
                : null,
            batchCandidates = solverOptions?.Algorithm == SolverAlgorithm.StateControl
                ? (int?)solverOptions.BatchCandidates
                : null,
            searchLanes = solverOptions?.Algorithm == SolverAlgorithm.StateControl
                ? (int?)solverOptions.SearchLanes
                : null,
            binaryBytePairMode = solverOptions?.Algorithm == SolverAlgorithm.StateControl
                ? "efm-state-input-level-and-current-dsv"
                : null,
            binaryBytePairs = solverOptions?.Algorithm == SolverAlgorithm.StateControl
                ? BinaryBytePairMetadata()
                : null,
            dispersionPool = solverOptions?.Algorithm == SolverAlgorithm.Dispersion
                ? DispersionPaletteMetadata()
                : null,
            channelPacking = "MSB-first, 1=land/white, 0=pit/black",
            imageMapping,
            iso,
        };
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(
                metadata,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                }));
    }

    private static void EnsureParentDirectory(string path)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static string DispersionPaletteDescription()
        => L.Format("dispersion black/white pool: Q={0}; nominal luminance=0,255; threshold=128",
            string.Join(",", Enumerable.Range(0, 2)
                .Select(level => $"0x{DvdGrayscalePalette.GetScrambledByteForLevel(level):X2}")));

    private static object DispersionPaletteMetadata()
        => FastDvdImageFileWriter.DispersionPaletteMetadata();

    private static object[] BinaryBytePairMetadata()
        => EfmPlusBinaryBytePairs.AtZeroDsv
            .Select(entry => (object)new
            {
                state = entry.State,
                inputLevel = entry.InitialIsLand ? "land" : "pit",
                blackByte = $"0x{entry.Pair.BlackByte:X2}",
                whiteByte = $"0x{entry.Pair.WhiteByte:X2}",
                blackWorstLandCount = entry.Pair.BlackWorstLandCount,
                whiteWorstLandCount = entry.Pair.WhiteWorstLandCount,
                referenceDsv = entry.Pair.ReferenceDsv,
                blackWorstAbsoluteOutputDsv = entry.Pair.BlackWorstAbsoluteOutputDsv,
                whiteWorstAbsoluteOutputDsv = entry.Pair.WhiteWorstAbsoluteOutputDsv,
            })
            .ToArray();

    private static void WriteJson(string path, object value)
    {
        EnsureParentDirectory(path);
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(
                value,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                }));
    }

    private static int RunSelfTests()
    {
        SelfTests.Run();
        return 0;
    }

    private static void PrintHelp()
    {
        Console.WriteLine(CommandHelp.Dvd);
        Console.WriteLine(L.T("Cli.LanguageHelp"));
    }
}
