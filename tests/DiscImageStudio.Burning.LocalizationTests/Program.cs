using System.Globalization;
using DiscImageStudio.Localization;

string original = L.PreferredLanguage;
CultureInfo numericCulture = CultureInfo.CurrentCulture;
try
{
    L.SetLanguage("zh-CN");
    BluRaySequentialWriterTests.Run();
    BluRaySequentialBurnTests.Run();
    BluRaySessionCleanupTests.Run();
    BluRayFlushDiagnosticsTests.Run();
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
    foreach (string language in new[] { "zh-CN", "zh-TW", "en", "ja", "tr" })
    {
        L.SetLanguage(language);
        BurningTranslationTests.Run();
        if (CultureInfo.CurrentCulture.Name != "tr-TR")
            throw new InvalidOperationException("Burning translations changed numeric culture.");
    }
    Console.WriteLine("burning translations: passed (five-language diagnostics, identical CD/BD bytes and MMC commands, stable stages, cancellation and cleanup; fake devices only)");
}
finally { L.Initialize(original); CultureInfo.CurrentCulture = numericCulture; }
