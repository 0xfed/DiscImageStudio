using System.Globalization;
using System.Text.Json.Nodes;
using DiscImageStudio.BluRay;
using DiscImageStudio.BluRay.Encoding;
using DiscImageStudio.Core;
using DiscImageStudio.ImageIO;
using DiscImageStudio.Localization;

internal static class ModuleTranslationTests
{
    internal static void Run(string root, string[] languages)
    {
        string directory = Path.Combine(Path.GetTempPath(), "disc-module-languages-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        string originalLanguage = L.PreferredLanguage;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            JsonNode? invariantInfo = null;
            byte[]? invariantIso = null;
            byte[]? invariantProbe = null;
            BluRayDiscModule module = new((_, _) => new BluRayGray8Raster(2, 2, [0, 64, 128, 255]));
            string fixture = Path.Combine(root, "src", "DiscImageStudio.App", "Assets", "DiscImageStudio-256.png");
            foreach (string language in languages)
            {
                L.SetLanguage(language);
                JsonNode info = JsonNode.Parse(Run(module, "bd-info", "--total-sectors", "1024").Summary!)!;
                Check(info["luminanceMapping"]!["note"]!.GetValue<string>()
                      == L.T("Run-count bias is not an optical-reflectance calibration."), "info note in " + language);
                Check(info["addressModel"]!.GetValue<string>()
                      == L.T("Nominal data zones; drive spare areas and defect remapping require calibration."), "address note in " + language);
                info["luminanceMapping"]!.AsObject().Remove("note");
                info.AsObject().Remove("addressModel");
                invariantInfo ??= info;
                Check(JsonNode.DeepEquals(invariantInfo, info), "JSON schema or technical values changed in " + language);

                DiscJobResult decoderRequired = Run(new BluRayDiscModule(), "bd-generate",
                    "--input", "virtual.png", "--output", Path.Combine(directory, "no-decoder.iso"),
                    "--total-sectors", "1024");
                Check(decoderRequired.ExitCode == 2 && decoderRequired.Summary == L.T(
                    "BD picture generation requires an image loader. Run bd-generate through the application or provide a BluRayGray8Raster loader."),
                    "missing image decoder guidance in " + language);

                List<DiscJobProgress> progress = [];
                string iso = Path.Combine(directory, language + ".iso");
                DiscJobResult generated = Run(module, "bd-generate",
                    ["--input", "virtual.png", "--output", iso, "--total-sectors", "1024"],
                    new ImmediateProgress(value => progress.Add(value)));
                Check(generated.Succeeded && generated.OutputPath == iso, "ISO result in " + language);
                Check(progress.Count > 0 && progress.All(p => p.Stage == "iso"
                    && p.Message == L.Format("BD {0}/{1} bytes", p.CompletedUnits, p.TotalUnits)), "progress in " + language);
                byte[] bytes = File.ReadAllBytes(iso);
                invariantIso ??= bytes;
                Check(bytes.SequenceEqual(invariantIso), "ISO payload changed in " + language);

                string probe = Path.Combine(directory, language + ".bin");
                DiscJobResult probed = Run(module, "bd-probe", "--output", probe, "--byte", "0xA6", "--sectors", "32");
                Check(probed.Succeeded && new FileInfo(probe).Length == 32 * 2048, "probe result in " + language);
                bytes = File.ReadAllBytes(probe);
                invariantProbe ??= bytes;
                Check(bytes.SequenceEqual(invariantProbe), "probe payload changed in " + language);

                Expect<ArgumentOutOfRangeException>(() => new BluRayDiscParameters(TotalSectors: 33).Validate(),
                    L.Format("BD-R total sectors must be a positive multiple of 32, no greater than {0}.", BluRayDiscParameters.SectorsPerLayer), "TotalSectors");
                Expect<ArgumentException>(() => BluRayEdc.WriteDataFrame(new byte[1], new byte[2052]),
                    L.T("A BD user-data frame contains exactly 2048 bytes."), "userData");
                Expect<ArgumentException>(() => RasterCodec.SavePng(Path.Combine(directory, "invalid.png"), 2, 2, [0], true),
                    L.T("Pixel buffer length does not match the dimensions and format."), "pixels");
                Expect<ArgumentException>(() => RasterCodec.ValidateSource(fixture, 1, long.MaxValue),
                    L.Format("The source image must be no larger than {0:G} MiB.", 1 / 1_048_576d), "path");
                Expect<ArgumentException>(() => RasterCodec.ValidateSource(fixture, long.MaxValue, 1024),
                    string.Format(CultureInfo.InvariantCulture,
                        L.T("The source image must contain between 1 and {0:N0} pixels."), 1024), "path");
                Check(CultureInfo.CurrentCulture.Name == "tr-TR", "numeric culture changed in " + language);
            }
            Console.WriteLine("module translations: passed (five-language diagnostics, stable JSON identifiers, identical BD ISO/probe bytes, source limits and numeric culture)");
        }
        finally
        {
            L.Initialize(originalLanguage);
            CultureInfo.CurrentCulture = originalCulture;
            Directory.Delete(directory, recursive: true);
        }
    }

    private static DiscJobResult Run(BluRayDiscModule module, string command, params string[] arguments)
        => Run(module, command, arguments, null);

    private static DiscJobResult Run(BluRayDiscModule module, string command, string[] arguments, IProgress<DiscJobProgress>? progress)
        => module.ExecuteAsync(new DiscJobRequest(command, arguments), progress).GetAwaiter().GetResult();

    private static void Expect<T>(Action action, string message, string parameter) where T : ArgumentException
    {
        try { action(); }
        catch (T exception)
        {
            Check(exception.ParamName == parameter && exception.Message.Contains(message, StringComparison.Ordinal),
                "localized exception content, type, or parameter changed: " + exception.Message);
            return;
        }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class ImmediateProgress(Action<DiscJobProgress> report) : IProgress<DiscJobProgress>
    {
        public void Report(DiscJobProgress value) => report(value);
    }
}
