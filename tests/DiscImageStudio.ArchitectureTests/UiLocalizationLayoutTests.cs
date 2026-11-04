using System.Globalization;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DiscImageStudio;
using DiscImageStudio.Burning;
using DiscImageStudio.Controls;
using DiscImageStudio.Core;
using DiscImageStudio.Localization;
using DiscImageStudio.Services;
using DiscImageStudio.ViewModels;

/// <summary>Runs in a fresh Windows process: WPF owns one Application and one STA.
/// Exercises real workspace bindings with temporary stores and a recorder that cannot write.</summary>
internal static class UiLocalizationLayoutTests
{
    private static readonly string[] Languages = ["zh-CN", "zh-TW", "en", "ja", "tr"];
    private static readonly (int Width, int Height, AppTheme Theme)[] Profiles =
        [(1366, 900, AppTheme.Dark), (960, 700, AppTheme.Light)];
    private sealed record Issue(string Scenario, string Element, string Text, string Reason);
    private sealed record Scenario(string Name, int TextBlocks, double Width, double Height, double DpiX, double DpiY);

    internal static void Run()
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try { CheckWindows(); }
            catch (Exception error) { failure = error; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromMinutes(8)))
            throw new TimeoutException("The isolated UI localization layout run exceeded eight minutes.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void CheckWindows()
    {
        string output = Path.GetFullPath(Environment.GetEnvironmentVariable("DISC_IMAGE_STUDIO_UI_SNAPSHOTS")
            ?? Path.Combine("artifacts", "ci", "localization-layout"));
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(new
        {
            Status = "running", Languages,
            Note = "A completed report replaces this file; an interrupted run is not a pass.",
        }));
        string temporary = Path.Combine(Path.GetTempPath(), "disc-ui-languages-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        string originalLanguage = L.PreferredLanguage;
        string originalCache = TempFiles.LivePreviewDirectory;
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        List<Issue> issues = [];
        List<Scenario> scenarios = [];
        Application? application = null;
        MainWindow? window = null;
        ShellViewModel? shell = null;
        string? fatal = null;
        try
        {
            Application.ResourceAssembly = typeof(MainWindow).Assembly;
            application = AppHost.CreateApplication(ShutdownMode.OnExplicitShutdown, AppTheme.Dark, L.Chinese);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            TempFiles.TryConfigureCacheDirectory(Path.Combine(temporary, "cache"));
            CheckInspectionFixtures();
            DiscParametersState state = new(Path.Combine(temporary, "disc-presets.json"));
            shell = new ShellViewModel(Dispatcher.CurrentDispatcher, new OfflineBurner(), state,
                new RecentProjectStore(Path.Combine(temporary, "recent-projects.json")));
            AppSettingsStore settingsStore = new(Path.Combine(temporary, "settings.json"));
            window = new MainWindow(new AppSettings { Language = L.Chinese, Theme = AppTheme.Dark }, settingsStore, shell)
            {
                WindowState = WindowState.Normal,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -10000, Top = -10000, ShowInTaskbar = false,
            };
            window.Show();
            Pump();
            foreach ((string family, long sectors, double inner, double outer) in new[]
            {
                ("cd", 359849L, 24.5, 56.8), ("dvd", 2295104L, 24.0, 58.0), ("bd", 12219392L, 24.0, 58.0),
            })
            {
                string projectPath = Path.Combine(temporary, family + ".discproj");
                ProjectDocument project = new(ProjectJsonStore.CurrentSchemaVersion, "Project 日本語 Türkçe", family,
                    new ProjectGeometry(inner, outer, sectors), [],
                    new ProjectOutputs(Path.Combine(temporary, family + (family == "cd" ? ".wav" : ".iso")),
                        true, "", "LAYOUT", true), null, null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
                ProjectJsonStore.Save(projectPath, project);
                byte[] saved = File.ReadAllBytes(projectPath);
                window.ConfigureSnapshot(Workspace.Design, null, projectPath: projectPath);
                foreach (string language in Languages)
                {
                    // Reuse already loaded pages so this also exercises actual window language-change wiring.
                    L.SetLanguage(language);
                    foreach (var profile in Profiles)
                    {
                        ThemeResolver.Apply(application, profile.Theme);
                        window.Width = profile.Width;
                        window.Height = profile.Height;
                        foreach (Workspace workspace in Enum.GetValues<Workspace>())
                        {
                            window.ConfigureSnapshot(workspace, null);
                            if (workspace == Workspace.Preview)
                            {
                                shell.LivePreview.SetPreviewMode(true, clearResult: false);
                                foreach (Expander expander in Descendants(window).OfType<Expander>()) expander.IsExpanded = false;
                            }
                            if (workspace == Workspace.Calibration) shell.Calibration.StepOneExpanded = true;
                            Pump();
                            if (workspace != Workspace.Home)
                            {
                                string titleKey = workspace switch
                                {
                                    Workspace.Design => "Xaml.Design", Workspace.Preview => "Xaml.Preview",
                                    Workspace.Export => "Xaml.Export", Workspace.Burn => "Xaml.Burn",
                                    Workspace.Calibration => "Xaml.Calibration2", _ => throw new InvalidOperationException(),
                                };
                                PageHeader header = Descendants(window).OfType<PageHeader>().Single(h => h.IsVisible);
                                if (header.Title != L.T(titleKey))
                                    throw new InvalidOperationException($"Loaded {workspace} header did not follow language {language}.");
                            }
                            string name = $"{language}-{family}-{profile.Width}-{profile.Theme}-{workspace}";
                            Capture(window, name);
                            if (workspace == Workspace.Preview)
                            {
                                foreach (Expander expander in Descendants(window).OfType<Expander>()) expander.IsExpanded = true;
                                Pump();
                                Capture(window, name + "-advanced");
                                shell.LivePreview.SetPreviewMode(false, clearResult: false);
                                Pump();
                                Capture(window, name + "-readback");
                                shell.LivePreview.SetPreviewMode(true, clearResult: false);
                            }
                            if (workspace == Workspace.Calibration)
                            {
                                shell.Calibration.StepTwoExpanded = true;
                                Pump();
                                Capture(window, name + "-photo");
                                shell.Calibration.StepThreeExpanded = true;
                                Pump();
                                Capture(window, name + "-trace");
                                shell.Calibration.StepOneExpanded = true;
                            }
                        }
                    }
                    if (!File.ReadAllBytes(projectPath).SequenceEqual(saved) || shell.CurrentProject?.Name != project.Name
                        || ProjectSession.CaptureGeometry(project, state) != project.Geometry)
                        throw new InvalidOperationException("UI language/layout changes altered saved project geometry or bytes.");
                    if (CultureInfo.CurrentCulture.Name != "tr-TR")
                        throw new InvalidOperationException("UI language changes altered numeric culture.");
                }
            }
            foreach (string language in Languages)
            {
                L.SetLanguage(language);
                SettingsWindow settings = new(new SettingsViewModel(new AppSettings { Language = language }, settingsStore))
                {
                    Width = 760, Height = 600, Left = -10000, Top = -10000, ShowInTaskbar = false,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                };
                try
                {
                    settings.Show();
                    for (int category = 0; category < 3; category++)
                    {
                        settings.ShowCategoryForSnapshot(category);
                        Pump();
                        Capture(settings, $"{language}-settings-{category}");
                    }
                }
                finally { settings.Close(); }
                foreach (bool edit in new[] { false, true })
                {
                    ProjectDialogViewModel model = new(state,
                        new ProjectDialogRequest(EditMode: edit, PreselectFamily: OpticalDiscFamily.Dvd),
                        new RecentTemplateStore(Path.Combine(temporary, "recent-templates.json")), temporary);
                    ProjectDialogWindow dialog = new(model)
                    {
                        Left = -10000, Top = -10000, ShowInTaskbar = false,
                        WindowStartupLocation = WindowStartupLocation.Manual,
                    };
                    try { dialog.Show(); Pump(); Capture(dialog, $"{language}-project-{(edit ? "edit" : "new")}"); }
                    finally { dialog.Close(); }
                }
            }
            if (scenarios.Count != 325 || scenarios.Any(scenario => scenario.TextBlocks < 5))
                throw new InvalidOperationException("The expected 325 UI layout scenarios did not all render meaningful text.");
            if (issues.Count != 0)
                throw new InvalidOperationException($"UI localization layout found {issues.Count} text issues; see {output}.");
            Console.WriteLine($"UI localization layout: {scenarios.Count} scenarios passed in five languages; screenshots: {output}");

            void Capture(Window target, string name)
            {
                FrameworkElement root = (FrameworkElement)target.Content;
                target.UpdateLayout();
                int count = Inspect(root, name, issues);
                DpiScale dpi = VisualTreeHelper.GetDpi(root);
                scenarios.Add(new(name, count, root.ActualWidth, root.ActualHeight, dpi.PixelsPerInchX, dpi.PixelsPerInchY));
                RenderTargetBitmap bitmap = new((int)Math.Ceiling(target.ActualWidth * dpi.DpiScaleX),
                    (int)Math.Ceiling(target.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
                bitmap.Render(target);
                PngBitmapEncoder encoder = new();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using FileStream stream = File.Create(Path.Combine(output, name + ".png"));
                encoder.Save(stream);
            }
        }
        catch (Exception error) { fatal = error.ToString(); throw; }
        finally
        {
            File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(new
            {
                Status = fatal is null ? "passed" : "failed", Languages, Scenarios = scenarios, Issues = issues, Fatal = fatal,
                Note = "Synthetic empty projects; actual DPI recorded per scenario; no physical recorder or package installation.",
            }, new JsonSerializerOptions { WriteIndented = true }));
            if (window is not null) { if (shell is not null) shell.IsDirty = false; window.Close(); }
            L.Initialize(originalLanguage);
            CultureInfo.CurrentCulture = originalCulture;
            TempFiles.TryConfigureCacheDirectory(originalCache);
            application?.Shutdown();
            Directory.Delete(temporary, recursive: true);
        }
    }

    private static int Inspect(FrameworkElement root, string scenario, List<Issue> issues)
    {
        int count = 0;
        foreach (TextBlock text in Descendants(root).OfType<TextBlock>())
        {
            if (!text.IsVisible || text.ActualWidth <= 0 || text.ActualHeight <= 0 || string.IsNullOrWhiteSpace(text.Text)) continue;
            count++;
            string label = string.IsNullOrWhiteSpace(text.Name) ? text.TemplatedParent?.GetType().Name ?? "TextBlock" : text.Name;
            string sample = text.Text.Length > 220 ? text.Text[..220] + "…" : text.Text;
            void Add(string reason) => issues.Add(new(scenario, label, sample, reason));
            // Ellipsis is intentional for paths and user names. Rich inline formatting uses
            // its actual glyph drawing below; a single-typeface estimate would mismeasure it.
            bool uniform = text.Inlines.All(inline => inline is Run run && run.FontSize == text.FontSize
                && run.FontWeight == text.FontWeight && run.FontStyle == text.FontStyle && run.FontFamily.Equals(text.FontFamily));
            if (text.TextTrimming == TextTrimming.None && uniform)
            {
                Thickness padding = text.Padding;
                double width = Math.Max(1, text.ActualWidth - padding.Left - padding.Right);
                double height = text.ActualHeight - padding.Top - padding.Bottom;
                FormattedText formatted = new(text.Text, text.Language.GetSpecificCulture(), text.FlowDirection,
                    new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch),
                    text.FontSize, text.Foreground, VisualTreeHelper.GetDpi(text).PixelsPerDip);
                if (text.TextWrapping != TextWrapping.NoWrap) formatted.MaxTextWidth = width;
                if (double.IsFinite(text.LineHeight)) formatted.LineHeight = text.LineHeight;
                const double tolerance = 2;
                if (formatted.Height > height + tolerance)
                    Add($"Text requires {formatted.Height:F1} DIP height but has {height:F1}.");
                if (formatted.WidthIncludingTrailingWhitespace > width + tolerance)
                    Add($"Text requires {formatted.WidthIncludingTrailingWhitespace:F1} DIP width but has {width:F1}.");
                Rect atRoot = text.TransformToAncestor(root).TransformBounds(new Rect(text.RenderSize));
                if (atRoot.Bottom > 0 && atRoot.Top < root.ActualHeight
                    && (atRoot.Left < -tolerance || atRoot.Right > root.ActualWidth + tolerance))
                    Add("Text extends horizontally outside the window content.");
            }
            DrawingGroup? drawing = VisualTreeHelper.GetDrawing(text);
            if (drawing is not null)
                foreach (GlyphRunDrawing glyphs in Drawings(drawing).OfType<GlyphRunDrawing>())
                    if (HasMissingGlyph(glyphs.GlyphRun))
                        Add("Rendered text contains a missing glyph.");
        }
        return count;
    }

    private static bool HasMissingGlyph(GlyphRun run)
    {
        if (run.Characters is not { } characters) return false;
        for (int i = 0; i < characters.Count; i++)
        {
            char character = characters[i];
            // Formatting controls and variation selectors can have no painted glyph.
            if (char.IsControl(character) || char.IsWhiteSpace(character)
                || char.GetUnicodeCategory(character) == UnicodeCategory.Format || character is >= '\uFE00' and <= '\uFE0F') continue;
            int glyph = run.ClusterMap is { Count: > 0 } clusters ? clusters[i]
                : run.GlyphIndices.Count == characters.Count ? i : -1;
            if (glyph >= 0 && glyph < run.GlyphIndices.Count && run.GlyphIndices[glyph] == 0) return true;
        }
        return false;
    }

    private static void CheckInspectionFixtures()
    {
        Grid root = new() { Width = 160, Height = 140 };
        TextBlock overflowing = new() { Text = "Long label that must not fit", Width = 40, Height = 18,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        root.Children.Add(overflowing);
        Window host = new() { Content = root, SizeToContent = SizeToContent.WidthAndHeight,
            Left = -10000, Top = -10000, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual };
        try
        {
            host.Show(); Pump();
            List<Issue> issues = [];
            Inspect(root, "overflow-fixture", issues);
            if (issues.Count == 0) throw new InvalidOperationException("Text-bound checking missed deliberately overflowing text.");
            overflowing.TextTrimming = TextTrimming.CharacterEllipsis;
            Pump(); issues.Clear(); Inspect(root, "ellipsis-fixture", issues);
            if (issues.Count != 0) throw new InvalidOperationException("Text-bound checking rejected intentional ellipsis.");
            overflowing.TextTrimming = TextTrimming.None;
            overflowing.Text = "A label wrapping onto several lines";
            overflowing.TextWrapping = TextWrapping.Wrap;
            Pump(); issues.Clear(); Inspect(root, "height-fixture", issues);
            if (!issues.Any(issue => issue.Reason.Contains("height", StringComparison.Ordinal)))
                throw new InvalidOperationException("Text-bound checking missed deliberately clipped wrapped text.");
            overflowing.Text = "\u0378"; // An unassigned scalar, absent from the normal UI fonts.
            overflowing.Width = 100;
            overflowing.Height = 60;
            Pump(); issues.Clear(); Inspect(root, "missing-glyph-fixture", issues);
            if (!issues.Any(issue => issue.Reason.Contains("missing glyph", StringComparison.Ordinal)))
                throw new InvalidOperationException("Glyph checking missed an unrenderable character.");
        }
        finally { host.Close(); }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (DependencyObject child in Descendants(VisualTreeHelper.GetChild(root, i))) yield return child;
    }

    private static IEnumerable<Drawing> Drawings(Drawing drawing)
    {
        yield return drawing;
        if (drawing is DrawingGroup group)
            foreach (Drawing child in group.Children)
                foreach (Drawing nested in Drawings(child)) yield return nested;
    }

    private static void Pump()
    {
        DispatcherFrame frame = new();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private sealed class OfflineBurner : IOpticalDiscBurner
    {
        public Task<IReadOnlyList<OpticalBurnDevice>> GetDevicesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<OpticalBurnDevice>>([]);
        public Task<IReadOnlyList<OpticalWriteSpeed>> GetSupportedWriteSpeedsAsync(string deviceId,
            OpticalBurnMediaKind mediaKind, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<OpticalWriteSpeed>>([]);
        public Task<OpticalBurnResult> BurnAsync(OpticalBurnRequest request, IProgress<OpticalBurnProgress>? progress = null,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The UI layout fixture cannot write to a recorder.");
    }
}
