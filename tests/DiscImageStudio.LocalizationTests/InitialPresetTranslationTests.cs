using System.Globalization;
using System.Text.Json;
using DiscImageStudio.Core;
using DiscImageStudio.Localization;

internal static class InitialPresetTranslationTests
{
    internal static void Run(string[] languages, Action<string, string> checkKey)
    {
        DiscPresetJsonDocument initial = DiscPresetJsonStore.CreateInitialUserDocument();
        var presets = initial.CdPresets.Select(preset => (OpticalDiscFamily.CompactDisc, preset.Id, preset.DisplayName, preset.Description))
            .Concat(initial.DvdPresets.Select(preset => (OpticalDiscFamily.Dvd, preset.Id, preset.DisplayName, preset.Description)))
            .Concat(initial.BluRayPresets!.Select(preset => (OpticalDiscFamily.BluRay, preset.Id, preset.DisplayName, preset.Description))).ToArray();
        foreach (var (_, _, name, description) in presets)
        {
            checkKey(name, "initial user preset name");
            checkKey(description, "initial user preset description");
        }
        string original = L.PreferredLanguage;
        CultureInfo numericCulture = CultureInfo.CurrentCulture;
        string saved = JsonSerializer.Serialize(initial);
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            foreach (string language in languages)
            {
                L.SetLanguage(language);
                foreach (var (family, id, name, description) in presets)
                {
                    string? originalName = PresetText.InitialDisplayName(family, id);
                    string? originalDescription = PresetText.InitialDescription(family, id);
                    Check(originalName == name && originalDescription == description, "initial fallback source");
                    Check(PresetText.Resolve(name, null, originalName) == L.T(name), "initial name follows " + language);
                    Check(PresetText.Description(description, null, originalDescription, 24, 58, 1000) == L.T(description), "initial description follows " + language);
                    Check(PresetText.Resolve("My İ-日本語", null, originalName) == "My İ-日本語", "edited name preserved");
                    Check(PresetText.Description("My notes: İ-日本語", null, originalDescription, 24, 58, 1000) == "My notes: İ-日本語", "edited description preserved");
                    Check(PresetText.Resolve(name, null, PresetText.InitialDisplayName(family, "another-user-id")) == name, "unrelated user text preserved");
                    foreach (OpticalDiscFamily other in Enum.GetValues<OpticalDiscFamily>().Where(value => value != family))
                        Check(PresetText.InitialDisplayName(other, id) is null && PresetText.InitialDescription(other, id) is null,
                            "initial IDs cannot capture user presets in another media family");
                }
                Check(JsonSerializer.Serialize(initial) == saved, "initial persisted values changed");
                Check(CultureInfo.CurrentCulture.Name == "tr-TR", "numeric culture changed");
            }
            Console.WriteLine("initial preset translations: passed (three default names/descriptions in five languages, preserved user edits, media-family IDs and persisted values)");
        }
        finally { L.Initialize(original); CultureInfo.CurrentCulture = numericCulture; }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
