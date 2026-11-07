using DiscImageStudio.Localization;
using System.Diagnostics;
using DiscImageStudio.Core;
using DiscImageStudio.Core.Calibration;
using DvdImageSolver;
using DvdImageSolver.Encoding;
using DvdImageSolver.Solver;

namespace DiscImageStudio.Dvd;

public sealed record DvdStreamingOptions(
    uint TotalSectors,
    double InnerRadiusMm,
    double OuterRadiusMm,
    double ChannelBitLengthNm = 133.3,
    double StartAngleDegrees = 0,
    byte LuminanceThreshold = 128,
    byte AlphaThreshold = 1,
    int RandomSeed = 1,
    int FastParallelism = 0,
    double PitchLinear = 0,
    double PitchQuadratic = 0,
    double PitchCubic = 0,
    ToneProfile? Tone = null,
    bool UseSampleReuse = true,
    bool ReadCompatibility = false,
    uint? UsableSectors = null,
    double? SourceImageOuterRadiusMm = null,
    string ReadCompatibilityMode = DvdReadCompatibilityModes.Current)
{
    /// <summary>Physical channel-bit length of the DVD scheme; the UI no longer exposes it.</summary>
    public const double StandardChannelBitLengthNm = 133.3;

    public uint OutputSectors => UsableSectors ?? TotalSectors;
    public long ContentLength => checked(
        (long)OutputSectors * DvdEccBlockEncoder.PayloadBytesPerSector);
    public double SamplingImageOuterRadiusMm => SourceImageOuterRadiusMm ?? OutputOuterRadiusMm;
    public double OutputOuterRadiusMm => OutputSectors == TotalSectors ? OuterRadiusMm
        : DvdTrackGeometry.Create(InnerRadiusMm, OuterRadiusMm,
            TotalSectors * (double)DvdEccBlockEncoder.ChannelBitsPerSector * ChannelBitLengthNm * 1e-6,
            PitchLinear, PitchQuadratic, PitchCubic).RadiusAtArcLength(
                OutputSectors * (double)DvdEccBlockEncoder.ChannelBitsPerSector * ChannelBitLengthNm * 1e-6);

    public void Validate()
    {
        if (ReadCompatibility && !DvdReadCompatibilityModes.IsSupported(ReadCompatibilityMode))
            throw new ArgumentException(L.T("Unsupported DVD read compatibility recipe."), nameof(ReadCompatibilityMode));
        DvdTrackGeometry.ValidatePitchCoefficients(PitchLinear, PitchQuadratic, PitchCubic);
        if (Tone is { } tone)
        {
            tone.Validate();
            if (!tone.IsDvdCompatible)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(Tone),
                    L.T("DVD dispersion uses two textures; tone levels must be 2."));
            }
        }
        if (TotalSectors == 0 || OutputSectors == 0 || OutputSectors > TotalSectors
            || OutputSectors % DvdEccBlockEncoder.SectorCount != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(TotalSectors),
                L.T("DVD streaming sector count must be a positive multiple of 16."));
        }

        if (!double.IsFinite(InnerRadiusMm)
            || !double.IsFinite(OuterRadiusMm)
            || InnerRadiusMm <= 0
            || OuterRadiusMm <= InnerRadiusMm)
        {
            throw new ArgumentOutOfRangeException(
                nameof(OuterRadiusMm),
                L.T("DVD outer radius must be greater than its positive inner radius."));
        }

        if (!double.IsFinite(ChannelBitLengthNm) || ChannelBitLengthNm <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ChannelBitLengthNm),
                L.T("DVD channel-bit length must be positive."));
        }

        if (!double.IsFinite(StartAngleDegrees))
        {
            throw new ArgumentOutOfRangeException(nameof(StartAngleDegrees));
        }

        if (FastParallelism is < 0 or > 64)
        {
            throw new ArgumentOutOfRangeException(
                nameof(FastParallelism),
                L.T("DVD streaming parallelism must be automatic (0) or 1..64."));
        }

        if (SourceImageOuterRadiusMm is { } sourceRadius && (!double.IsFinite(sourceRadius) || sourceRadius <= 0))
            throw new ArgumentOutOfRangeException(nameof(SourceImageOuterRadiusMm), L.T("Source image radius must be positive and finite."));
        _ = ContentLength;
        // Resolve the complete curve before a hybrid stream writes its filesystem
        // prefix. Positive coefficients alone do not guarantee numerical resolution.
        double trackLengthMm = TotalSectors * (double)DvdEccBlockEncoder.ChannelBitsPerSector
            * ChannelBitLengthNm * 1e-6;
        _ = DvdTrackGeometry.Create(InnerRadiusMm, OuterRadiusMm, trackLengthMm,
            PitchLinear, PitchQuadratic, PitchCubic);
    }
}

public sealed record DvdStreamingProgress(
    long CompletedBytes,
    long TotalBytes,
    int CompletedBlocks,
    int TotalBlocks,
    TimeSpan Elapsed)
{
    public double Fraction => TotalBytes == 0 ? 0 : (double)CompletedBytes / TotalBytes;
}

public sealed record DvdStreamingSummary(
    string SourceImage,
    uint Sectors,
    long BytesWritten,
    long ControlledWords,
    TimeSpan Elapsed,
    long CompatibilityChanges = 0);

public sealed class DvdHybridStreamingPlan
{
    internal DvdHybridStreamingPlan(
        HybridIsoImageWriter.LayoutPlan isoLayout,
        uint drawingStartLba,
        uint drawingEndLbaExclusive)
    {
        IsoLayout = isoLayout;
        DrawingStartLba = drawingStartLba;
        DrawingEndLbaExclusive = drawingEndLbaExclusive;
    }

    internal HybridIsoImageWriter.LayoutPlan IsoLayout { get; }

    public string SourceDirectory => IsoLayout.SourceDirectory;

    public uint TotalSectors => IsoLayout.VolumeSectors;

    public long ContentLength => (long)IsoLayout.RecordedSectors * 2048;

    public bool UseUdf250 => IsoLayout.UseUdf250;

    public uint FilesystemEndLbaExclusive => IsoLayout.DataEndLbaExclusive;

    public uint DrawingStartLba { get; }

    public uint DrawingEndLbaExclusive { get; }

    public bool FilesAtOuterRing => IsoLayout.FilesAtOuterRing;

    public uint DrawingSectors => checked(DrawingEndLbaExclusive - DrawingStartLba);

    public int FileCount => IsoLayout.FileCount;

    public int DirectoryCount => IsoLayout.DirectoryCount;

    public long FileBytes => IsoLayout.FileBytes;
}

public static class DvdStreamingGenerator
{
    private const uint PsnOffset = 0x30000;

    public static DvdHybridStreamingPlan PrepareHybrid(
        string dataDirectory,
        DvdStreamingOptions options,
        string volumeLabel = "DVD_IMAGE",
        bool filesAtOuterRing = false, bool useUdf250 = false, uint updateReserveSectors = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        HybridIsoImageWriter.LayoutPlan isoLayout = HybridIsoImageWriter.Plan(
            dataDirectory,
            options.OutputSectors,
            volumeLabel,
            filesAtOuterRing, useUdf250, updateReserveSectors);
        uint drawingStartLba = AlignToEccBlock(isoLayout.DrawingStartLba, PsnOffset);
        uint drawingEndLba = isoLayout.DrawingEndLbaExclusive / 16 * 16;
        if (drawingEndLba < drawingStartLba || drawingEndLba - drawingStartLba < DvdEccBlockEncoder.SectorCount)
        {
            throw new ArgumentException(
                L.T("The filesystem leaves no complete ECC Block for drawing in the selected file layout."),
                nameof(dataDirectory));
        }

        return new DvdHybridStreamingPlan(isoLayout, drawingStartLba, drawingEndLba);
    }

    public static DvdStreamingSummary Generate(
        string imagePath,
        Stream output,
        DvdStreamingOptions options,
        Action<DvdStreamingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        string sourceFullPath = Path.GetFullPath(imagePath);
        MultiBlockSolveOptions solveOptions = CreateSolveOptions(
            options,
            startLba: 0,
            fillSectors: options.OutputSectors);
        FastDvdStreamSummary summary = FastDispersionImageWriter.WriteStream(
            sourceFullPath,
            output,
            solveOptions,
            value => progress?.Invoke(new DvdStreamingProgress(
                value.BytesWritten,
                value.TotalBytes,
                value.CompletedBlocks,
                value.TotalBlocks,
                value.Elapsed)),
            cancellationToken);
        return new DvdStreamingSummary(
            sourceFullPath,
            summary.SectorsWritten,
            summary.BytesWritten,
            summary.ControlledWords,
            summary.Elapsed,
            summary.CompatibilityChanges);
    }

    /// <summary>
    /// Generates calibration payload sectors directly from an analytic grayscale
    /// pattern. Coordinates are millimetres from the disc centre, x right and y down.
    /// The callback must support concurrent calls when parallel generation is enabled.
    /// </summary>
    public static DvdStreamingSummary GeneratePattern(
        Func<double, double, byte> sampleGray,
        Stream output,
        DvdStreamingOptions options,
        Action<DvdStreamingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sampleGray);
        return GeneratePattern(sampleGray, null, output, options, progress, cancellationToken);
    }

    public static DvdStreamingSummary GeneratePattern(
        CalibrationTarget target, Stream output, DvdStreamingOptions options,
        Action<DvdStreamingProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(options);
        if (target.Parameters.Kind != CalibrationDiscKind.Dvd || target.Parameters.Sectors != options.TotalSectors)
            throw new ArgumentException(L.T("Calibration target and output must describe the same DVD sector capacity."), nameof(target));
        // A calibration target describes the bytes that were generated/burned.
        // Later changes to the project output checkbox must not change that target.
        options = options with
        {
            ReadCompatibility = target.Parameters.DvdReadCompatibility,
            ReadCompatibilityMode = target.EffectiveDvdReadCompatibilityMode,
            UsableSectors = checked((uint)Math.Min(options.OutputSectors, target.Parameters.OutputSectors)),
        };
        if (target.IsVisualSelectorPattern)
            return GenerateCalibrationPattern(target, output, progress, options.FastParallelism, cancellationToken,
                options.OutputSectors);
        if (target.UsesReferencePattern)
        {
            ArgumentNullException.ThrowIfNull(output);
            options.Validate();
            MultiBlockSolveOptions solveOptions = CreateSolveOptions(options, 0, options.OutputSectors);
            FastDvdStreamSummary summary = FastDispersionImageWriter.WriteCalibrationReferenceStream(
                target, output, solveOptions,
                value => progress?.Invoke(new DvdStreamingProgress(value.BytesWritten, value.TotalBytes,
                    value.CompletedBlocks, value.TotalBlocks, value.Elapsed)), cancellationToken);
            return new DvdStreamingSummary("calibration", summary.SectorsWritten, summary.BytesWritten,
                summary.ControlledWords, summary.Elapsed, summary.CompatibilityChanges);
        }
        if (!target.HasWideAddressBands)
            return GeneratePattern(target.Sample, output, options, progress, cancellationToken);

        var bands = target.WideBandSectorRanges;
        var angleSweep = target.HasAngleSweep ? new CalibrationAngleSweepSampler(target) : null;
        byte SectorLuminance(uint sector)
        {
            if (angleSweep is not null && angleSweep.TryGetLuminance((sector + 0.5) / options.TotalSectors, out var marker))
                return marker;
            for (int i = 0; i < bands.Count; i++)
                if (sector >= bands[i].Start && sector < bands[i].EndExclusive)
                    return 0;
            return 255;
        }
        return GeneratePattern(target.Sample, SectorLuminance, output, options, progress, cancellationToken);
    }

    private static DvdStreamingSummary GeneratePattern(
        Func<double, double, byte> sampleGray, Func<uint, byte>? sectorLuminance,
        Stream output, DvdStreamingOptions options,
        Action<DvdStreamingProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        MultiBlockSolveOptions solveOptions = CreateSolveOptions(
            options,
            startLba: 0,
            fillSectors: options.OutputSectors);
        void Report(FastDvdStreamProgress value) => progress?.Invoke(new DvdStreamingProgress(
            value.BytesWritten, value.TotalBytes, value.CompletedBlocks, value.TotalBlocks, value.Elapsed));
        FastDvdStreamSummary summary = sectorLuminance is null
            ? FastDispersionImageWriter.WritePatternStream(sampleGray, output, solveOptions, Report, cancellationToken)
            : FastDispersionImageWriter.WriteSectorPatternStream(sectorLuminance, output, solveOptions, Report, cancellationToken);
        return new DvdStreamingSummary(
            "calibration",
            summary.SectorsWritten,
            summary.BytesWritten,
            summary.ControlledWords,
            summary.Elapsed,
            summary.CompatibilityChanges);
    }

    /// <summary>Encodes the immutable calibration record. Visual selectors are sampled
    /// directly at payload codeword positions, avoiding raster aliasing and layout scaling.</summary>
    public static DvdStreamingSummary GenerateCalibrationPattern(
        CalibrationTarget target,
        Stream output,
        Action<DvdStreamingProgress>? progress = null,
        int fastParallelism = 0,
        CancellationToken cancellationToken = default,
        uint? usableSectors = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(output);
        target.Validate();
        CalibrationParameters p = target.Parameters;
        if (p.Kind != CalibrationDiscKind.Dvd)
            throw new ArgumentException(L.T("DVD calibration requires a DVD target."), nameof(target));
        DvdStreamingOptions options = new(checked((uint)p.Sectors), p.InnerRadiusMm, p.OuterRadiusMm,
            p.LinearDensity, FastParallelism: fastParallelism,
            PitchLinear: p.PitchLinear, PitchQuadratic: p.PitchQuadratic, PitchCubic: p.PitchCubic,
            ReadCompatibility: p.DvdReadCompatibility,
            UsableSectors: checked((uint)Math.Min(usableSectors ?? p.OutputSectors, p.OutputSectors)),
            ReadCompatibilityMode: target.EffectiveDvdReadCompatibilityMode);
        options.Validate();
        if (target.VisualSelector is not { } selector)
            return GeneratePattern(target, output, options, progress, cancellationToken);

        double totalChannelBits = p.Sectors * (double)CalibrationParameters.DvdChannelBitsPerSector;
        FastDvdStreamSummary summary = FastDispersionImageWriter.WriteCodewordPatternStream(
            sample => selector.SampleRecordingProgress((sample.GlobalCodewordIndex * 16.0 + 8) / totalChannelBits),
            output, CreateSolveOptions(options, 0, options.OutputSectors),
            value => progress?.Invoke(new DvdStreamingProgress(value.BytesWritten, value.TotalBytes,
                value.CompletedBlocks, value.TotalBlocks, value.Elapsed)), cancellationToken);
        return new DvdStreamingSummary("calibration", summary.SectorsWritten, summary.BytesWritten,
            summary.ControlledWords, summary.Elapsed, summary.CompatibilityChanges);
    }

    public static DvdStreamingSummary GenerateHybrid(
        string imagePath,
        Stream output,
        DvdStreamingOptions options,
        DvdHybridStreamingPlan plan,
        Action<DvdStreamingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(plan);
        options.Validate();
        if (plan.TotalSectors != options.OutputSectors)
        {
            throw new ArgumentException(
                L.T("Hybrid layout and DVD streaming options describe different disc sizes."),
                nameof(plan));
        }

        string sourceFullPath = Path.GetFullPath(imagePath);
        Stopwatch stopwatch = Stopwatch.StartNew();
        if (plan.FilesAtOuterRing)
            HybridIsoImageWriter.WriteOuterRingPrefix(plan.IsoLayout, output, plan.DrawingStartLba, cancellationToken);
        else
            HybridIsoImageWriter.WriteSequentialPrefix(plan.IsoLayout, output, plan.DrawingStartLba, cancellationToken);
        long prefixBytes = checked(
            (long)plan.DrawingStartLba * DvdEccBlockEncoder.PayloadBytesPerSector);
        int totalBlocks = checked((int)(options.OutputSectors / DvdEccBlockEncoder.SectorCount));
        progress?.Invoke(new DvdStreamingProgress(
            prefixBytes,
            plan.ContentLength,
            checked((int)(plan.DrawingStartLba / DvdEccBlockEncoder.SectorCount)),
            totalBlocks,
            stopwatch.Elapsed));

        MultiBlockSolveOptions solveOptions = CreateSolveOptions(
            options,
            plan.DrawingStartLba,
            plan.DrawingSectors);
        FastDvdStreamSummary drawing = FastDispersionImageWriter.WriteStream(
            sourceFullPath,
            output,
            solveOptions,
            value => progress?.Invoke(new DvdStreamingProgress(
                checked(prefixBytes + value.BytesWritten),
                plan.ContentLength,
                checked((int)(plan.DrawingStartLba / DvdEccBlockEncoder.SectorCount)
                    + value.CompletedBlocks),
                totalBlocks,
                stopwatch.Elapsed)),
            cancellationToken);
        if (plan.FilesAtOuterRing || plan.UseUdf250)
        {
            HybridIsoImageWriter.WriteOuterRingSuffix(plan.IsoLayout, output, plan.DrawingEndLbaExclusive, cancellationToken);
            progress?.Invoke(new DvdStreamingProgress(plan.ContentLength, plan.ContentLength,
                totalBlocks, totalBlocks, stopwatch.Elapsed));
        }
        stopwatch.Stop();
        return new DvdStreamingSummary(
            sourceFullPath,
            options.OutputSectors,
            plan.ContentLength,
            drawing.ControlledWords,
            stopwatch.Elapsed,
            drawing.CompatibilityChanges);
    }

    internal static MultiBlockSolveOptions CreateSolveOptions(
        DvdStreamingOptions options,
        uint startLba,
        uint fillSectors)
    {
        ImageMappingOptions imageMapping = new(
            options.TotalSectors,
            options.InnerRadiusMm,
            options.OuterRadiusMm,
            options.ChannelBitLengthNm,
            options.StartAngleDegrees,
            Clockwise: true,
            options.LuminanceThreshold,
            options.AlphaThreshold,
            SampleEveryChannelBits: 1,
            PitchLinear: options.PitchLinear,
            PitchQuadratic: options.PitchQuadratic,
            PitchCubic: options.PitchCubic,
            Tone: options.Tone,
            UsableSectors: options.UsableSectors,
            ImageOuterRadiusMm: options.SourceImageOuterRadiusMm
                ?? (options.UsableSectors.HasValue ? options.OutputOuterRadiusMm : null));
        return new MultiBlockSolveOptions(
            startLba,
            fillSectors,
            PsnOffset,
            IterationsPerBlock: 1,
            MutationBytes: 4,
            RandomSeed: options.RandomSeed,
            InitialTemperature: 64.0,
            FinalTemperature: 0.25,
            ZeroInitialPayload: false,
            imageMapping,
            Algorithm: SolverAlgorithm.Dispersion,
            FastParallelism: options.FastParallelism,
            UseSampleReuse: options.UseSampleReuse,
            ReadCompatibility: options.ReadCompatibility,
            ReadCompatibilityMode: options.ReadCompatibilityMode);
    }

    private static uint AlignToEccBlock(uint lba, uint psnOffset)
    {
        uint remainder = (lba + psnOffset) & (DvdEccBlockEncoder.SectorCount - 1);
        return remainder == 0
            ? lba
            : checked(lba + DvdEccBlockEncoder.SectorCount - remainder);
    }
}
