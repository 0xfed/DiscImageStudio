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
        Size workspaceSize = new(1366, 900);
        string? fatal = null;
        try
        {
            application = AppHost.CreateApplication(ShutdownMode.OnExplicitShutdown, AppTheme.Dark, L.Chinese);
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            TempFiles.TryConfigureCacheDirectory(Path.Combine(temporary, "cache"));
            CheckInspectionFixtures(issues);
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
                        workspaceSize = new(profile.Width, profile.Height);
                        FrameworkElement content = (FrameworkElement)window.Content;
                        content.Width = profile.Width;
                        content.Height = profile.Height;
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
                            if (workspace == Workspace.Burn)
                            {
                                if (!shell.Burn.CanConfigureFileUpdates
                                    || !Descendants(window).OfType<CheckBox>().Any(box => box.IsVisible
                                        && Equals(box.Content, L.T("Xaml.KeepDiscOpenForFileUpdates"))))
                                    throw new InvalidOperationException($"File-update checkbox is missing for {family}/{language}.");
                            }
                            if (workspace == Workspace.Design && family != ProjectDocument.BluRayFamily
                                && !Descendants(window).OfType<CheckBox>().Any(box => box.IsVisible
                                    && Equals(box.Content, L.T("Xaml.UseUdf250Vat"))))
                                throw new InvalidOperationException($"UDF format checkbox is missing for {family}/{language}.");
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
                // Page and disclosure transitions take up to 250 ms. Inspect the
                // settled state instead of sampling a partially revealed panel.
                DispatcherFrame frame = new();
                DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(300) };
                timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
                timer.Start();
                try { Dispatcher.PushFrame(frame); }
                finally { timer.Stop(); }
                target.UpdateLayout();
                if (ReferenceEquals(target, window))
                {
                    // CI's virtual monitor can cap the native HWND at 1044x788.
                    // Lay out the loaded content at the requested viewport size;
                    // render that visual directly, retaining the real host DPI.
                    root.Measure(workspaceSize);
                    root.Arrange(new Rect(workspaceSize));
                    root.UpdateLayout();
                    root.Measure(workspaceSize);
                    root.Arrange(new Rect(workspaceSize));
                    if (Math.Abs(root.ActualWidth - workspaceSize.Width) > 1
                        || Math.Abs(root.ActualHeight - workspaceSize.Height) > 1)
                        throw new InvalidOperationException("The layout viewport did not reach the requested dimensions.");
                }
                DpiScale dpi = VisualTreeHelper.GetDpi(root);
                RenderTargetBitmap bitmap = new((int)Math.Ceiling(root.ActualWidth * dpi.DpiScaleX),
                    (int)Math.Ceiling(root.ActualHeight * dpi.DpiScaleY), dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
                DrawingVisual background = new();
                using (DrawingContext context = background.RenderOpen())
                    context.DrawRectangle(target.Background ?? Brushes.Transparent, null, new Rect(root.RenderSize));
                bitmap.Render(background);
                bitmap.Render(root);
                int count = Inspect(root, name, issues);
                scenarios.Add(new(name, count, root.ActualWidth, root.ActualHeight, dpi.PixelsPerInchX, dpi.PixelsPerInchY));
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
            DrawingGroup? drawing = VisualTreeHelper.GetDrawing(text);
            // Inspect the actual glyph layout, including mixed runs and layout
            // rounding. Reformatting at ActualWidth can invent an extra line.
            // Ellipsis and the recent-project card's scrolling fade are intentional.
            bool scrollingPath = text.Name == "PART_LocationText" && text.TemplatedParent is RecentProjectCard;
            if (text.TextTrimming == TextTrimming.None && !scrollingPath && drawing is not null && !drawing.Bounds.IsEmpty)
            {
                Rect visible = new(text.RenderSize);
                if (VisualTreeHelper.GetClip(text) is Geometry clip) visible.Intersect(clip.Bounds);
                if (visible.IsEmpty) continue;
                const double tolerance = 2;
                Rect ink = drawing.Bounds;
                if (ink.Top < visible.Top - tolerance || ink.Bottom > visible.Bottom + tolerance)
                    Add($"Rendered text exceeds visible height: ink={ink}, visible={visible}.");
                if (ink.Left < visible.Left - tolerance || ink.Right > visible.Right + tolerance)
                    Add($"Rendered text exceeds visible width: ink={ink}, visible={visible}.");
                Rect atRoot = text.TransformToAncestor(root).TransformBounds(ink);
                if (atRoot.Bottom > 0 && atRoot.Top < root.ActualHeight
                    && (atRoot.Left < -tolerance || atRoot.Right > root.ActualWidth + tolerance))
                    Add("Text extends horizontally outside the window content.");
            }
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

    private static void CheckInspectionFixtures(List<Issue> failures)
    {
        Grid root = new() { Width = 160, Height = 140 };
        TextBlock overflowing = new() { Text = "Long label that must not fit", Width = 40, Height = 18,
            FontFamily = new FontFamily("Segoe UI"), FontSize = 14, Foreground = Brushes.Black,
            TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.None,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        root.Children.Add(overflowing);
        Window host = new() { Content = root, SizeToContent = SizeToContent.WidthAndHeight,
            Left = -10000, Top = -10000, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual };
        try
        {
            host.Show(); RenderFixture();
            List<Issue> issues = [];
            int count = Inspect(root, "overflow-fixture", issues);
            Require(count > 0 && issues.Any(issue => issue.Reason.Contains("width", StringComparison.Ordinal)),
                "overflow-fixture", "Text-bound checking missed deliberately overflowing text.");
            overflowing.TextTrimming = TextTrimming.CharacterEllipsis;
            RenderFixture(); issues.Clear(); Inspect(root, "ellipsis-fixture", issues);
            Require(issues.Count == 0, "ellipsis-fixture", "Text-bound checking rejected intentional ellipsis.");
            overflowing.TextTrimming = TextTrimming.None;
            overflowing.Text = "A label wrapping onto several lines";
            overflowing.TextWrapping = TextWrapping.Wrap;
            RenderFixture(); issues.Clear(); Inspect(root, "height-fixture", issues);
            Require(issues.Any(issue => issue.Reason.Contains("height", StringComparison.Ordinal)),
                "height-fixture", "Text-bound checking missed deliberately clipped wrapped text.");
            overflowing.Text = "\u0378"; // An unassigned scalar, absent from the normal UI fonts.
            overflowing.Width = 100;
            overflowing.Height = 60;
            RenderFixture(); issues.Clear(); Inspect(root, "missing-glyph-fixture", issues);
            Require(issues.Any(issue => issue.Reason.Contains("missing glyph", StringComparison.Ordinal)),
                "missing-glyph-fixture", "Glyph checking missed an unrenderable character.");

            void Require(bool condition, string scenario, string reason)
            {
                if (condition) return;
                FormattedText formatted = new(overflowing.Text, CultureInfo.GetCultureInfo("en-US"), FlowDirection.LeftToRight,
                    new Typeface(overflowing.FontFamily, overflowing.FontStyle, overflowing.FontWeight, overflowing.FontStretch),
                    overflowing.FontSize, overflowing.Foreground, 1);
                if (overflowing.TextWrapping != TextWrapping.NoWrap) formatted.MaxTextWidth = overflowing.ActualWidth;
                string inlines = string.Join("; ", overflowing.Inlines.Select(inline => inline is Run run
                    ? $"Run(font={run.FontFamily.Source}, size={run.FontSize}, weight={run.FontWeight}, text={run.Text})"
                    : inline.GetType().Name));
                failures.Add(new(scenario, "fixture", overflowing.Text,
                    reason + $" visible={overflowing.IsVisible}, size={overflowing.ActualWidth}x{overflowing.ActualHeight}, "
                    + $"clip={VisualTreeHelper.GetClip(overflowing)?.Bounds}, "
                    + $"ink={VisualTreeHelper.GetDrawing(overflowing)?.Bounds}, "
                    + $"wrap={overflowing.TextWrapping}, trim={overflowing.TextTrimming}, "
                    + $"formatted={formatted.WidthIncludingTrailingWhitespace}x{formatted.Height}, inlines=[{inlines}], "
                    + $"issues=[{string.Join("; ", issues.Select(issue => issue.Reason))}]."));
            }
        }
        finally { host.Close(); }

        void RenderFixture()
        {
            Pump();
            host.UpdateLayout();
            RenderTargetBitmap bitmap = new(160, 140, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
        }
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
