using System.Globalization;
using System.Text;
using DiscImageStudio.Cd;
using DiscImageStudio.Core;
using DiscImageStudio.Core.Calibration;
using DiscImageStudio.Iso;
using DiscImageStudio.Localization;

internal static class ValidationTranslationTests
{
    internal static void Run(string[] languages)
    {
        string directory = Path.Combine(Path.GetTempPath(), "disc-validation-languages-" + Guid.NewGuid().ToString("N"));
        string source = Path.Combine(directory, "日本語-Türkçe-中文");
        Directory.CreateDirectory(source);
        string input = Path.Combine(source, "日本語-Türkçe-中文.txt");
        File.WriteAllText(input, "User content: 日本語 Türkçe 中文", Encoding.UTF8);
        DateTimeOffset timestamp = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        ProjectDocument project = new(ProjectJsonStore.CurrentSchemaVersion, "My project 日本語 Türkçe", "cd",
            new(24.5, 56.8, 359849), [new("layer-İ-日本語", "My layer 中文", input, true, 0, 0, 0, 1)],
            new(Path.Combine(directory, "çıkış.wav"), true, source, "My volume", true), null, null, timestamp, timestamp);
        string originalLanguage = L.PreferredLanguage;
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            byte[]? invariantProject = null;
            Dictionary<string, byte[]> invariantVolumes = [];
            foreach (string language in languages)
            {
                L.SetLanguage(language);
                string path = Path.Combine(directory, "project.discproj");
                ProjectJsonStore.Save(path, project);
                byte[] bytes = File.ReadAllBytes(path);
                invariantProject ??= bytes;
                Check(bytes.SequenceEqual(invariantProject), "project data changed in " + language);
                ProjectDocument loaded = ProjectJsonStore.Load(path);
                Check(loaded.Name == project.Name && loaded.Layers.Single() == project.Layers.Single(), "user content changed in " + language);

                Expect<InvalidDataException>(() => ProjectJsonStore.Validate(project with { SchemaVersion = 99 }),
                    L.Format("不支持 schemaVersion={0}；当前支持 {1} 与 {2}。", 99, 1, 2));
                Expect<InvalidDataException>(() => ProjectJsonStore.Validate(project with { DiscFamily = "user-İ-日本語" }),
                    L.Format("discFamily '{0}' 无效；仅支持 '{1}'、'{2}' 与 '{3}'。", "user-İ-日本語", "cd", "dvd", "bd"));
                Expect<InvalidDataException>(() => ProjectJsonStore.Validate(project with
                {
                    Layers = [project.Layers[0] with { TranslateXMm = double.NaN }],
                }), L.Format("{0} 必须是有限数值。", L.Format("图层 '{0}' 的 translateXMm", project.Layers[0].Id)));
                DiscPresetJsonDocument presets = DiscPresetJsonStore.LoadBuiltIn();
                foreach (var (context, invalidDisabled) in new[]
                {
                    ("disabled CD", presets with { DisabledCdPresetIds = [" "] }),
                    ("disabled DVD", presets with { DisabledDvdPresetIds = [" "] }),
                    ("disabled Blu-ray", presets with { DisabledBluRayPresetIds = [" "] }),
                })
                    Expect<InvalidDataException>(() => DiscPresetJsonStore.Validate(invalidDisabled),
                        L.Format("预设 '{0}' 的 {1} {2}。", L.T(context), "id", L.T("不能为空")));
                CdDiscPresetDefinition invalidPreset = presets.CdPresets[0] with { InnerRadiusMm = double.NaN };
                Expect<InvalidDataException>(() => DiscPresetJsonStore.Validate(presets with { CdPresets = [invalidPreset] }),
                    L.Format("预设 '{0}' 的 {1} {2}。", invalidPreset.Id, "innerRadiusMm", L.T("必须是大于 0 的有限数值")));
                Expect<InvalidDataException>(() => DiscPresetJsonStore.Validate(presets with
                {
                    CdPresets = [presets.CdPresets[0] with { Sectors = 0 }],
                }), L.Format("预设 '{0}' 的 {1} {2}。", presets.CdPresets[0].Id, "sectors", L.T("必须大于 0")));
                BluRayDiscPresetDefinition invalidBluRay = presets.BluRayPresets![0] with { TotalSectors = 33 };
                Expect<InvalidDataException>(() => DiscPresetJsonStore.Validate(presets with { BluRayPresets = [invalidBluRay] }),
                    L.Format("预设 '{0}' 的 {1} {2}。", invalidBluRay.Id, "totalSectors",
                        L.Format("必须是 32 的倍数且不超过 {0:N0}", ProjectDocument.BluRaySectorsPerLayer)));
                Expect<ArgumentOutOfRangeException>(() => new CdDiscParameters(24.5, 56.8, 0).Validate(),
                    L.T("CD sector count must be positive."), "Sectors");
                Expect<ArgumentOutOfRangeException>(() => new ToneProfile(double.NaN, ToneProfile.TwoLevels, 0).Validate(),
                    L.Format("{0}.ditherStrength must be a finite number between 0 and 1.", "tone"), "tone");
                Expect<ArgumentOutOfRangeException>(() => Iso9660ImageWriter.Plan(source, 1, "My volume"),
                    L.T("Hybrid ISO volume is too small for filesystem metadata."), "volumeSectors");
                Expect<ArgumentOutOfRangeException>(() => UdfImageWriter.Plan(source, 33, "My volume"),
                    L.Format("UDF drawing volumes need at least {0} sectors and a multiple of {1} sectors.", UdfImageWriter.MinimumVolumeSectors, 32), "volumeSectors");
                CalibrationPhotoFitResult fit = CalibrationPhotoRectifier.Fit([], []);
                Check(!fit.Succeeded && fit.Message == L.T("外缘和内缘各需要至少 5 个分散的点，且每条轮廓不超过 256 个点。"),
                    "photo rectification guidance in " + language);

                foreach (bool outer in new[] { false, true })
                {
                    Iso9660ImageWriter.LayoutPlan iso = Iso9660ImageWriter.Plan(source, 1024, "My volume", timestamp, outer);
                    using MemoryStream isoStream = new();
                    if (outer)
                    {
                        Iso9660ImageWriter.WriteOuterRingPrefix(iso, isoStream, iso.FileDataStartLba);
                        Iso9660ImageWriter.WriteOuterRingSuffix(iso, isoStream, iso.FileDataStartLba);
                    }
                    else Iso9660ImageWriter.WriteSequentialPrefix(iso, isoStream, iso.VolumeSectors);
                    CompareVolume("ISO-" + outer, isoStream.ToArray());

                    UdfImageWriter.LayoutPlan udf = UdfImageWriter.Plan(source, 1024, "My volume", timestamp, outer);
                    using MemoryStream udfStream = new();
                    UdfImageWriter.WriteSequentialPrefix(udf, udfStream, udf.DrawingEndLbaExclusive);
                    UdfImageWriter.WriteSequentialSuffix(udf, udfStream, udf.DrawingEndLbaExclusive);
                    CompareVolume("UDF-" + outer, udfStream.ToArray());
                }
                Check(CultureInfo.CurrentCulture.Name == "tr-TR", "numeric culture changed in " + language);
            }
            Console.WriteLine("validation translations: passed (project/preset/calibration/CD/ISO errors, user data, and identical inner/outer ISO9660/Joliet/UDF bytes in five languages)");

            void CompareVolume(string name, byte[] bytes)
            {
                Check(bytes.Length == 1024 * 2048, name + " output length");
                if (invariantVolumes.TryGetValue(name, out byte[]? expected))
                    Check(bytes.SequenceEqual(expected), name + " changed in " + L.CurrentLanguage);
                else invariantVolumes.Add(name, bytes);
            }
        }
        finally
        {
            L.Initialize(originalLanguage);
            CultureInfo.CurrentCulture = originalCulture;
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void Expect<T>(Action action, string message, string? parameter = null) where T : Exception
    {
        try { action(); }
        catch (T exception)
        {
            bool matches = parameter is null ? exception.Message == message
                : exception is ArgumentException argument && argument.ParamName == parameter
                  && argument.Message.Contains(message, StringComparison.Ordinal);
            Check(matches, "localized exception type, content, or parameter changed: " + exception.Message);
            return;
        }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
