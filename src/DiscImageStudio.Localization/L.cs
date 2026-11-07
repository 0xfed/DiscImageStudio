using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace DiscImageStudio.Localization;

/// <summary>Application text. The saved preference is applied before any windows are
/// constructed and can be switched again at runtime through <see cref="SetLanguage"/>; XAML
/// text follows through <see cref="LanguageChanged"/>. Numeric culture is deliberately left
/// alone so engineering parameters and file formats retain their rules.</summary>
public static class L
{
    public const string Chinese = "zh-CN";
    public const string TraditionalChinese = "zh-TW";
    public const string English = "en";
    public const string Japanese = "ja";
    public const string Turkish = "tr";
    public const string Spanish = "es";
    public const string Czech = "cs";
    public const string SystemLanguage = "system";

    private static readonly CultureInfo StartupCulture = CultureInfo.CurrentUICulture;
    // Load text after language selection, so startup catalog failures use the selected language.
    private static readonly Lazy<IReadOnlyDictionary<string, Dictionary<string, string>>> Catalog = new(LoadCatalog);

    public static string CurrentLanguage { get; private set; } = Chinese;

    /// <summary>The normalized selection, including "system" when following the OS.
    /// Keep this separate from the resolved language when presenting settings.</summary>
    public static string PreferredLanguage { get; private set; } = Chinese;

    /// <summary>Raised after <see cref="SetLanguage"/> resolves to a different language. Views
    /// rebuilt from XAML bindings refresh themselves; cached text in view models listens too.</summary>
    public static event EventHandler? LanguageChanged;

    public static string NormalizeLanguage(string? language) => language?.Trim().ToLowerInvariant() switch
    {
        "system" => SystemLanguage,
        "en" => English,
        string name when name.StartsWith("en-", StringComparison.Ordinal) => English,
        "ja" => Japanese,
        string name when name.StartsWith("ja-", StringComparison.Ordinal) => Japanese,
        "tr" => Turkish,
        string name when name.StartsWith("tr-", StringComparison.Ordinal) => Turkish,
        "es" => Spanish,
        string name when name.StartsWith("es-", StringComparison.Ordinal) => Spanish,
        "cs" => Czech,
        string name when name.StartsWith("cs-", StringComparison.Ordinal) => Czech,
        string name when IsTraditionalChinese(name) => TraditionalChinese,
        "zh-cn" or "zh-sg" or "zh" or "zh-hans" or "zh-chs" => Chinese,
        string name when name.StartsWith("zh-hans-", StringComparison.Ordinal) => Chinese,
        _ => SystemLanguage,
    };

    public static string ResolveLanguage(string? language, CultureInfo? systemCulture = null)
    {
        string normalized = NormalizeLanguage(language);
        if (normalized != SystemLanguage) return normalized;

        CultureInfo culture = systemCulture ?? StartupCulture;
        if (culture.TwoLetterISOLanguageName == "ja") return Japanese;
        if (culture.TwoLetterISOLanguageName == "tr") return Turkish;
        if (culture.TwoLetterISOLanguageName == "es") return Spanish;
        if (culture.TwoLetterISOLanguageName == "cs") return Czech;
        if (culture.TwoLetterISOLanguageName != "zh") return English;
        return IsTraditionalChinese(culture.Name) ? TraditionalChinese : Chinese;
    }

    private static bool IsTraditionalChinese(string name) =>
        name.Equals("zh-TW", StringComparison.OrdinalIgnoreCase)
        || name.Equals("zh-HK", StringComparison.OrdinalIgnoreCase)
        || name.Equals("zh-MO", StringComparison.OrdinalIgnoreCase)
        || name.Equals("zh-CHT", StringComparison.OrdinalIgnoreCase)
        || name.Equals("zh-Hant", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("zh-Hant-", StringComparison.OrdinalIgnoreCase);

    public static void Initialize(string? language) => ApplyLanguage(language, notify: false);

    /// <summary>Switches the UI language in place. Existing windows refresh through
    /// <see cref="LanguageChanged"/>; nothing is recreated, so unsaved work survives.</summary>
    public static void SetLanguage(string? language) => ApplyLanguage(language, notify: true);

    private static void ApplyLanguage(string? language, bool notify)
    {
        PreferredLanguage = NormalizeLanguage(language);
        string resolved = ResolveLanguage(PreferredLanguage);
        bool changed = !string.Equals(CurrentLanguage, resolved, StringComparison.Ordinal);
        CurrentLanguage = resolved;
        CultureInfo culture = CultureInfo.GetCultureInfo(resolved);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        if (notify && changed)
        {
            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    public static bool TryGetText(string key, out string text)
    {
        ArgumentNullException.ThrowIfNull(key);
        // Entry-point catch handlers must still report a failed main catalog.
        if (key == "Cli.Error")
        {
            text = BootstrapText(key);
            return true;
        }
        if (Catalog.Value.TryGetValue(key, out Dictionary<string, string>? translations)
            && ((translations.TryGetValue(CurrentLanguage, out string? translated) && !string.IsNullOrWhiteSpace(translated))
                || (translations.TryGetValue(English, out translated) && !string.IsNullOrWhiteSpace(translated))
                || (translations.TryGetValue(Chinese, out translated) && !string.IsNullOrWhiteSpace(translated))))
        {
            text = translated;
            return true;
        }
        text = key;
        return false;
    }

    public static string T(string key)
    {
        return TryGetText(key, out string text) ? text : key;
    }

    public static string F(FormattableString text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Format(text.Format, text.GetArguments());
    }

    /// <summary>Translates a known diagnostic before inserting values. Unmarked
    /// exceptions retain the existing exact-key lookup and original fallback.</summary>
    public static string Error(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return DiagnosticMessage.Template(exception) is { } template
            ? Diagnostic(template)
            : T(exception.Message);
    }

    internal static string Diagnostic(FormattableString template)
        => Format(template.Format, template.GetArguments()
            .Select(value => value switch
            {
                FormattableString nested => Diagnostic(nested),
                DiagnosticMessage.ExceptionDetail detail => Error(detail.Exception),
                _ => value,
            }).ToArray());

    public static string Format(string key, params object?[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, T(key), arguments);

    internal static IReadOnlyDictionary<string, Dictionary<string, string>> Entries => Catalog.Value;

    private static IReadOnlyDictionary<string, Dictionary<string, string>> LoadCatalog()
    {
        Assembly assembly = typeof(L).Assembly;
        return LoadCatalog(assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith("DiscImageStudio.Localization.Catalogs.", StringComparison.Ordinal)
                           && name.EndsWith(".json", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => (name, assembly.GetManifestResourceStream(name)!)));
    }

    internal static IReadOnlyDictionary<string, Dictionary<string, string>> LoadCatalog(
        IEnumerable<(string Name, Stream Content)> catalogs)
    {
        Dictionary<string, Dictionary<string, string>> result = new(StringComparer.Ordinal);
        foreach ((string name, Stream content) in catalogs)
        {
            using Stream stream = content;
            var entries = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(stream)
                          ?? throw CatalogError("Empty localization catalog: {0}", name);
            foreach (var entry in entries)
            {
                if (!result.TryAdd(entry.Key, entry.Value))
                    throw CatalogError("Duplicate localization key: {0}", entry.Key);
            }
        }
        return result;
    }

    private static InvalidDataException CatalogError(string key, params object?[] arguments)
        => new(string.Format(CultureInfo.CurrentCulture, BootstrapText(key), arguments));

    private static string BootstrapText(string key)
    {
        // Do not call T while the main catalog is loading. Bootstrap diagnostics
        // have a dedicated embedded resource and share the normal audit.
        using Stream stream = typeof(L).Assembly.GetManifestResourceStream(
            "DiscImageStudio.Localization.Catalogs.CatalogLoader.json")!;
        var errors = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(stream)!;
        Dictionary<string, string> translations = errors[key];
        return translations.TryGetValue(CurrentLanguage, out string? value) ? value
            : translations.TryGetValue(English, out value) ? value : translations[Chinese];
    }
}
