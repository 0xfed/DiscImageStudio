using System.Globalization;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DiscImageStudio.DitherTool;
using DiscImageStudio.Imaging;
using DiscImageStudio.Localization;

internal static class DitherToolLocalizationTests
{
    internal static void Run()
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try { CheckLanguages(); }
            catch (Exception error) { failure = error; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        // XAML localization tracks targets weakly. Reclaim the closed windows
        // before later tests switch language on a different STA dispatcher.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        if (failure is not null) throw new InvalidOperationException("Dither tool localization failed.", failure);
        Console.WriteLine("dither-tool-localization: five-language window labels, gamma/mode changes, diagnostics and identical pixel/PNG bytes passed");
    }

    private static void CheckLanguages()
    {
        string original = L.PreferredLanguage;
        CultureInfo culture = CultureInfo.CurrentCulture;
        string directory = Path.Combine(Path.GetTempPath(), "dither-languages-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            const int width = 19, height = 11;
            byte[] pixels = Enumerable.Range(0, width * height * 4).Select(i => (byte)(i * 43 % 256)).ToArray();
            byte[] levels = [0, 255];
            BitmapSource source = BitmapSource.Create(width, height, 144, 144, PixelFormats.Bgra32, null, pixels, width * 4);
            Dictionary<bool, (byte[] Pixels, byte[] Png)> expected = [];
            foreach (string language in new[] { L.Chinese, L.TraditionalChinese, L.English, L.Japanese, L.Turkish })
            {
                L.Initialize(language);
                DitherWindow window = new();
                try
                {
                    Check(window.Title == L.T("黑白抖动"), "window title in " + language);
                    Check(((Button)window.FindName("OpenButton")).Content.Equals(L.T("打开图片")), "open action in " + language);
                    Check(((Button)window.FindName("SaveButton")).Content.Equals(L.T("保存黑白 PNG")), "save action in " + language);
                    ((ComboBox)window.FindName("ModePicker")).SelectedIndex = 1;
                    Check(((TextBlock)window.FindName("ModeHint")).Text == L.T("0D → 黑色 0 · AA → 白色 255"), "CD palette hint");
                    Check(((TextBlock)window.FindName("ResultHeading")).Text == L.Format("{0}抖动结果", L.T("CD 二阶")), "CD heading");
                    ((CheckBox)window.FindName("GammaToggle")).IsChecked = true;
                    Check(((TextBlock)window.FindName("GammaHint")).Text == L.T("已开启：按线性亮度分配黑白像素，中间调通常更深"), "gamma hint");
                    Check(((TextBlock)window.FindName("StatusText")).Text == L.Format("就绪 · {0}", L.T("sRGB 伽马：开")), "gamma status");
                }
                finally { window.Close(); }
                foreach (bool gamma in new[] { false, true })
                {
                    byte[] raw = GrayscaleDither.ProcessPixels(pixels, width, height, levels, linearizeSrgb: gamma);
                    BitmapSource result = GrayscaleDither.Process(source, levels, linearizeSrgb: gamma);
                    string path = Path.Combine(directory, "Türkçe-日本語-" + language + "-" + gamma + ".png");
                    GrayscaleDither.SavePng(result, path, levels);
                    byte[] png = File.ReadAllBytes(path);
                    if (expected.TryGetValue(gamma, out var baseline))
                        Check(raw.SequenceEqual(baseline.Pixels) && png.SequenceEqual(baseline.Png), "pixel or PNG output changed in " + language);
                    else expected.Add(gamma, (raw, png));
                }
                try { GrayscaleDither.ProcessPixels([0], 1, 1, levels); throw new InvalidOperationException("Invalid pixels accepted"); }
                catch (ArgumentException error)
                {
                    Check(error.ParamName == "bgra" && error.Message.Contains(L.T("The BGRA buffer must contain exactly width × height × 4 bytes."), StringComparison.Ordinal), "pixel diagnostic");
                }
                Check(CultureInfo.CurrentCulture.Name == "tr-TR", "numeric culture changed");
            }
        }
        finally
        {
            L.Initialize(original);
            CultureInfo.CurrentCulture = culture;
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
