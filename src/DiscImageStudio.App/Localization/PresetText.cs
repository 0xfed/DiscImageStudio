using System.Globalization;
using DiscImageStudio.Core;

namespace DiscImageStudio.Localization;

/// <summary>Translates preset metadata without changing persisted or user-authored text.</summary>
internal static class PresetText
{
    private static readonly DiscPresetJsonDocument Initial = DiscPresetJsonStore.CreateInitialUserDocument();
    internal const string UserDescriptionKey = "DiscPreset.UserDescription";

    internal static string? InitialDisplayName(OpticalDiscFamily family, string id) => family switch
    {
        OpticalDiscFamily.CompactDisc => Initial.CdPresets.FirstOrDefault(preset => preset.Id == id)?.DisplayName,
        OpticalDiscFamily.Dvd => Initial.DvdPresets.FirstOrDefault(preset => preset.Id == id)?.DisplayName,
        OpticalDiscFamily.BluRay => Initial.BluRayPresets?.FirstOrDefault(preset => preset.Id == id)?.DisplayName,
        _ => null,
    };

    internal static string? InitialDescription(OpticalDiscFamily family, string id) => family switch
    {
        OpticalDiscFamily.CompactDisc => Initial.CdPresets.FirstOrDefault(preset => preset.Id == id)?.Description,
        OpticalDiscFamily.Dvd => Initial.DvdPresets.FirstOrDefault(preset => preset.Id == id)?.Description,
        OpticalDiscFamily.BluRay => Initial.BluRayPresets?.FirstOrDefault(preset => preset.Id == id)?.Description,
        _ => null,
    };

    internal static string UserDescriptionFallback(double inner, double outer, long sectors) =>
        string.Create(CultureInfo.InvariantCulture,
            $"用户预设：内半径 {inner:0.###} mm、外半径 {outer:0.###} mm、{sectors:N0} 扇区。");

    internal static string Resolve(string text, string? resourceKey, string? builtInText)
    {
        if (resourceKey is not null && L.TryGetText(resourceKey, out string translated)) return translated;
        return text == builtInText ? L.T(text) : text;
    }

    internal static string Description(string text, string? resourceKey, string? builtInText,
        double inner, double outer, long sectors)
    {
        // Recognize the exact generated fallback, including old presets without a key.
        // Editing the description turns it into user content, even if the key is retained.
        if (resourceKey is null or UserDescriptionKey)
        {
            return text == UserDescriptionFallback(inner, outer, sectors)
                ? L.Format(UserDescriptionKey, inner, outer, sectors)
                : text == builtInText ? L.T(text) : text;
        }

        return Resolve(text, resourceKey, builtInText);
    }
}
