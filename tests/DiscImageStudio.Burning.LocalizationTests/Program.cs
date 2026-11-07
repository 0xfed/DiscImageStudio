using System.Globalization;
using DiscImageStudio.Localization;

string original = L.PreferredLanguage;
CultureInfo numericCulture = CultureInfo.CurrentCulture;
try
{
    L.SetLanguage("zh-CN");
    OpticalReadSessionTests.Run();
    UdfFilesystemTests.Run();
    UdfVatUpdateTests.Run();
    BluRayFileUpdateBurnTests.Run();
SequentialFileUpdateBurnTests.Run();
    BluRaySequentialWriterTests.Run();
    BluRaySequentialBurnTests.Run();
    BluRaySessionCleanupTests.Run();
    BluRayFlushDiagnosticsTests.Run();
    DeviceDiagnosticsTests.Run();
    CdDaoDetailedDiagnosticsTests.Run();
    DvdDetailedDiagnosticsTests.Run();
    BluRayDetailedDiagnosticsTests.Run();
    BurnStreamDiagnosticsTests.Run();
    CdBurnPhaseDiagnosticsTests.Run();
    OpticalBurnDiagnosticsTests.Run();
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
