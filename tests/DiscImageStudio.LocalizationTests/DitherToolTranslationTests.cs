using System.Globalization;
using DiscImageStudio.DitherTool;
using DiscImageStudio.Localization;

internal static class DitherToolTranslationTests
{
    internal static void Run(string[] languages)
    {
        string original = L.PreferredLanguage;
        CultureInfo culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            foreach (string language in languages)
            {
                string[] remaining = CommandLineLanguage.Initialize(["--language", language, "my-image-İ-日本語.png"]);
                Check(remaining.SequenceEqual(new[] { "my-image-İ-日本語.png" }), "positional image or language changed");
                foreach ((string name, DitherMode mode, string key) in new[]
                {
                    ("dVd", DitherMode.Dvd, "DVD 二阶"), ("cD", DitherMode.Cd, "CD 二阶"),
                })
                {
                    Check(DitherModes.Parse(name) == mode, "mode parsing changed");
                    Check(DitherModes.Label(mode) == L.T(key), "mode label in " + language);
                    Check(DitherModes.Levels(mode).SequenceEqual(new byte[] { 0, 255 }), "output palette changed");
                    Check(DitherModes.PaletteLabel(mode) == "0 / 255", "palette label changed");
                    Check(DitherModes.Suffix(mode) == "黑白抖动", "default output filename changed");
                }
                try { DitherModes.Parse("invalid-mode"); throw new InvalidOperationException("Invalid mode accepted"); }
                catch (ArgumentException error)
                {
                    Check(error.Message == L.T("--mode 请选择 dvd 或 cd，均输出黑白二阶。"), "mode diagnostic in " + language);
                }
                string help = L.T("Dither.Help");
                foreach (string token in new[] { "FourLevelDither [IMAGE]", "--input IMAGE --output RESULT.png", "--mode dvd|cd", "--gamma none|srgb", "0/255", "A5/92", "0D/AA", "Floyd–Steinberg" })
                    Check(help.Contains(token, StringComparison.Ordinal), "changed tool help syntax or palette: " + token);
                Check(CultureInfo.CurrentCulture.Name == "tr-TR", "numeric culture changed");
            }
            Console.WriteLine("dither tool translations: passed (five-language labels/help/errors, invariant mode parsing, palettes and filenames)");
        }
        finally { L.Initialize(original); CultureInfo.CurrentCulture = culture; }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
