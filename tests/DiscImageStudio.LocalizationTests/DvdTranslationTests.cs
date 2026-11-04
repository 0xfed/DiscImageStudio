using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;
using DiscImageStudio.Core;
using DiscImageStudio.Dvd;
using DiscImageStudio.ImageIO;
using DiscImageStudio.Localization;
using DvdImageSolver.Encoding;
using DvdImageSolver.Solver;

internal static class DvdTranslationTests
{
    internal static void Run(string[] languages)
    {
        string directory = Path.Combine(Path.GetTempPath(), "dvd-languages-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string image = Path.Combine(directory, "原图-İ-日本語.png");
        RasterCodec.SavePng(image, 2, 2, [0, 85, 170, 255], true);
        string payloadPath = Path.Combine(directory, "payload-İ-日本語.bin");
        byte[] payload = Enumerable.Range(0, DvdEccBlockEncoder.PayloadBytesPerBlock).Select(i => (byte)(i * 37 + 11)).ToArray();
        File.WriteAllBytes(payloadPath, payload);
        string invalidTarget = Path.Combine(directory, "target.txt");
        File.WriteAllText(invalidTarget, "İ");
        string streamingFiles = Path.Combine(directory, "stream-files-日本語-İ");
        Directory.CreateDirectory(Path.Combine(streamingFiles, "资料"));
        File.WriteAllBytes(Path.Combine(streamingFiles, "root.bin"), new byte[2591]);
        File.WriteAllBytes(Path.Combine(streamingFiles, "资料", "中文.bin"), new byte[65547]);
        File.WriteAllBytes(Path.Combine(streamingFiles, "empty.txt"), []);
        string language = L.PreferredLanguage;
        CultureInfo culture = CultureInfo.CurrentCulture;
        TextWriter output = Console.Out;
        TextWriter error = Console.Error;
        MethodInfo run = typeof(DvdDiscModule).Assembly.GetType("DvdImageSolver.Program")!
            .GetMethod("RunCommand", BindingFlags.Static | BindingFlags.NonPublic)!;
        MethodInfo compare = typeof(DvdDiscModule).Assembly.GetType("DvdImageSolver.SelfTests")!
            .GetMethod("Equal", BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(typeof(int));
        DvdDiscModule module = new();
        byte[]? encodedBaseline = null, isoBaseline = null;
        JsonNode? metadataBaseline = null;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            foreach (string selected in languages)
            {
                L.SetLanguage(selected);
                DvdStreamingOptions streaming = new(256, 24, 58, FastParallelism: 1);
                Expect<ArgumentOutOfRangeException>(() => (streaming with { Tone = new ToneProfile(0, 4, 0) }).Validate(),
                    L.T("DVD dispersion uses two textures; tone levels must be 2."), "Tone");
                Expect<ArgumentOutOfRangeException>(() => (streaming with { TotalSectors = 17 }).Validate(),
                    L.T("DVD streaming sector count must be a positive multiple of 16."), "TotalSectors");
                Expect<ArgumentOutOfRangeException>(() => (streaming with { OuterRadiusMm = 24 }).Validate(),
                    L.T("DVD outer radius must be greater than its positive inner radius."), "OuterRadiusMm");
                Expect<ArgumentOutOfRangeException>(() => (streaming with { ChannelBitLengthNm = 0 }).Validate(),
                    L.T("DVD channel-bit length must be positive."), "ChannelBitLengthNm");
                Expect<ArgumentOutOfRangeException>(() => (streaming with { FastParallelism = 65 }).Validate(),
                    L.T("DVD streaming parallelism must be automatic (0) or 1..64."), "FastParallelism");
                Expect<ArgumentException>(() => DvdStreamingGenerator.PrepareHybrid(streamingFiles,
                        streaming with { TotalSectors = 64 }, "UNCHANGED", true),
                    L.T("The filesystem leaves no complete ECC Block for drawing in the selected file layout."), "dataDirectory");
                DvdHybridStreamingPlan streamingPlan = DvdStreamingGenerator.PrepareHybrid(streamingFiles, streaming, "UNCHANGED");
                using MemoryStream rejectedStream = new();
                Expect<ArgumentException>(() => DvdStreamingGenerator.GenerateHybrid(image, rejectedStream,
                        streaming with { TotalSectors = 512 }, streamingPlan),
                    L.T("Hybrid layout and DVD streaming options describe different disc sizes."), "plan");
                Check(rejectedStream.Length == 0, "invalid hybrid plan wrote output");
                DvdEccBlockEncoder encoder = new();
                Expect<ArgumentException>(() => encoder.Encode([0], 0, 0x30000, new(1, 0, true)),
                    L.Format("An ECC Block needs exactly {0} payload bytes.", DvdEccBlockEncoder.PayloadBytesPerBlock), "payloads");
                Expect<ArgumentOutOfRangeException>(() => ModulationBoundary.Validate(0, 0, true),
                    L.T("EFMPlus state must be 1..4."), "state");
                Expect<FormatException>(() => ImageConstraint.Load(invalidTarget, 0),
                    L.Format("Unsupported target character '{0}'. Use 0/P, 1/L, or ?.", 'İ'));
                try
                {
                    compare.Invoke(null, [1, 2, L.T("channel bit count")]);
                    throw new InvalidOperationException("Selftest mismatch was accepted");
                }
                catch (TargetInvocationException failure) when (failure.InnerException is InvalidOperationException)
                {
                    Check(failure.InnerException.Message == L.Format("selftest failed: {0}",
                        L.Format("{0}: expected {1}, got {2}", L.T("channel bit count"), 1, 2)),
                        "selftest description or expected/actual values lost their language");
                }

                string encoded = Path.Combine(directory, selected + "-İ-日本語.bin");
                using StringWriter captured = new();
                using StringWriter errors = new();
                Console.SetOut(captured);
                Console.SetError(errors);
                int exit = (int)run.Invoke(null, [new[] { "encode", "--input", payloadPath, "--output", encoded }, false])!;
                Console.SetOut(output);
                Console.SetError(error);
                Check(exit == 0, "actual DVD encode failed: " + errors);
                byte[] bytes = File.ReadAllBytes(encoded);
                encodedBaseline ??= bytes;
                Check(bytes.Length == DvdEccBlockEncoder.ChannelBitsPerBlock / 8 && bytes.SequenceEqual(encodedBaseline),
                    "packed DVD channel data changed in " + selected);
                Check(captured.ToString().Contains(L.Format("encoded {0} payload bytes into {1} channel levels ({2} packed bytes)",
                    payload.Length, DvdEccBlockEncoder.ChannelBitsPerBlock, bytes.Length), StringComparison.Ordinal),
                    "encode result not translated in " + selected);
                JsonNode metadata = JsonNode.Parse(File.ReadAllText(encoded + ".json"))!;
                metadataBaseline ??= metadata;
                Check(JsonNode.DeepEquals(metadataBaseline, metadata), "DVD encode JSON schema or technical values changed");

                string iso = Path.Combine(directory, selected + "-原图.iso");
                captured.GetStringBuilder().Clear();
                Console.SetOut(captured);
                Console.SetError(errors);
                DiscJobResult generated = module.ExecuteAsync(new DiscJobRequest("solve",
                ["--image", image, "--iso-output", iso, "--total-sectors", "32", "--fill-sectors", "32",
                    "--fast-output", "true", "--fast-parallelism", "1", "--inner-radius-mm", "24.0",
                    "--outer-radius-mm", "58.0", "--channel-bit-nm", "133.3"])).GetAwaiter().GetResult();
                Console.SetOut(output);
                Console.SetError(error);
                Check(generated.Succeeded && generated.OutputPath == iso, "DVD generation failed: " + errors);
                bytes = File.ReadAllBytes(iso);
                isoBaseline ??= bytes;
                Check(bytes.Length == 32 * 2048 && bytes.SequenceEqual(isoBaseline), "DVD ISO bytes changed in " + selected);
                Check(captured.ToString().Contains(L.Format("fast raw ISO fill: LBA {0}..{1}, ECC blocks={2}, algorithm=dispersion, ECC/EFM replay=skipped, workers={3}",
                    0, 31, 2, 1), StringComparison.Ordinal), "DVD generation status not translated");
                Check(captured.ToString().Contains(iso, StringComparison.Ordinal), "Unicode output path changed");
                Check(CultureInfo.CurrentCulture.Name == "tr-TR", "DVD display language changed numeric culture");
            }
            Check(File.ReadAllBytes(payloadPath).SequenceEqual(payload), "encoding changed the source payload");
            Console.WriteLine("DVD translations: passed (five-language help/errors/results, identical packed EFMPlus data/JSON and ISO bytes, unchanged parameters and numeric culture)");
        }
        finally
        {
            Console.SetOut(output); Console.SetError(error);
            L.Initialize(language); CultureInfo.CurrentCulture = culture;
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void Expect<T>(Action action, string message, string? parameter = null) where T : Exception
    {
        try { action(); }
        catch (T exception)
        {
            Check(parameter is null ? exception.Message == message
                : exception is ArgumentException argument && argument.ParamName == parameter
                  && exception.Message.Contains(message, StringComparison.Ordinal), "localized DVD error changed: " + exception.Message);
            return;
        }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
