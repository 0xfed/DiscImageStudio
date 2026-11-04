using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using DiscImageStudio.Localization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

MediaUdfTests.Run();
CalibrationPatternTests.Run();
CalibrationRingTests.Run();
CalibrationVisualSelectorTests.Run();
CalibrationVisualEncodingTests.Run();

string root = AppContext.BaseDirectory;
while (!File.Exists(Path.Combine(root, "DiscImageStudio.slnx")))
    root = Directory.GetParent(root)?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
string app = Path.Combine(root, "src", "DiscImageStudio.App");
string[] languages = [L.Chinese, L.TraditionalChinese, L.English, L.Japanese, L.Turkish];
int references = 0;

void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

void CheckKey(string key, string location)
{
    Require(L.Entries.ContainsKey(key), $"Missing localization key in {location}: {key}");
    references++;
}

// Parse the original JSON as well: deserializing a dictionary alone hides duplicate keys.
HashSet<string> keys = new(StringComparer.Ordinal);
foreach (string file in Directory.GetFiles(Path.Combine(root, "src", "DiscImageStudio.Localization", "Catalogs"), "*.json", SearchOption.AllDirectories))
{
    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file));
    foreach (JsonProperty entry in document.RootElement.EnumerateObject())
    {
        Require(keys.Add(entry.Name), $"Duplicate catalog key: {entry.Name}");
        Require(L.Entries.ContainsKey(entry.Name), $"Source entry is absent from embedded resources: {entry.Name}");
        Dictionary<string, string> embedded = L.Entries[entry.Name];
        HashSet<string> seenLanguages = new(StringComparer.Ordinal);
        foreach (JsonProperty translation in entry.Value.EnumerateObject())
        {
            Require(seenLanguages.Add(translation.Name), $"Duplicate language for {entry.Name}: {translation.Name}");
            Require(translation.Value.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(translation.Value.GetString()), $"Empty translation: {entry.Name}/{translation.Name}");
            Require(embedded.TryGetValue(translation.Name, out string? value) && value == translation.Value.GetString(),
                $"Embedded translation differs from source: {entry.Name}/{translation.Name}");
        }
        Require(languages.All(seenLanguages.Contains), $"Incorrect language coverage: {entry.Name}");
        Require(embedded.Count == seenLanguages.Count, $"Embedded language set differs from source: {entry.Name}");
    }
}
Require(keys.SetEquals(L.Entries.Keys), "Embedded catalogs differ from source catalogs.");

foreach ((string key, Dictionary<string, string> translations) in L.Entries)
{
    HashSet<string> expected = ReadArguments(translations[L.Chinese]);
    foreach ((string language, string text) in translations)
    {
        Require(expected.SetEquals(ReadArguments(text)), $"Changed format arguments: {key}/{language}");
        // A translated label must not change the extensions accepted by a file dialog.
        if (translations[L.Chinese].Contains("|*", StringComparison.Ordinal))
        {
            string[] sourceParts = translations[L.Chinese].Split('|');
            string[] translatedParts = text.Split('|');
            Require(sourceParts.Length == translatedParts.Length
                && sourceParts.Where((_, i) => i % 2 == 1).SequenceEqual(translatedParts.Where((_, i) => i % 2 == 1)),
                $"Changed file dialog patterns: {key}/{language}");
        }
    }
}

// Syntax trees handle escaped strings, nested interpolation, alignment, and format clauses.
// Dynamic keys (presets, trigger values, calibration warnings) are checked separately below.
foreach (string file in new[] { "src", "tools" }.SelectMany(directory =>
             Directory.GetFiles(Path.Combine(root, directory), "*.cs", SearchOption.AllDirectories)).Where(IsSourceFile))
{
    SyntaxNode syntax = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
    string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
    if (relative.StartsWith("src/DiscImageStudio.Burning/", StringComparison.Ordinal))
    {
        foreach (ExpressionSyntax text in syntax.DescendantNodes().OfType<ExpressionSyntax>()
                     .Where(node => node is InterpolatedStringExpressionSyntax || node.IsKind(SyntaxKind.StringLiteralExpression)))
        {
            string value = text is InterpolatedStringExpressionSyntax interpolated ? FormatKey(interpolated)
                : ((LiteralExpressionSyntax)text).Token.ValueText;
            if (!value.Any(character => character is >= '\u4e00' and <= '\u9fff')) continue;
            Require(text.Ancestors().OfType<InvocationExpressionSyntax>()
                .Any(call => call.Expression.ToString() is "L.T" or "L.F" or "L.Format"),
                "Unlocalized burning text in " + relative + ": " + value);
        }
    }
    if (new[] { "DiscImageStudio.App", "DiscImageStudio.Burning", "DiscImageStudio.BluRay", "DiscImageStudio.ImageIO", "DiscImageStudio.Core", "DiscImageStudio.Cd", "DiscImageStudio.Dvd", "DiscImageStudio.Iso", "DiscImageStudio.Imaging" }
        .Any(project => relative.StartsWith("src/" + project + "/", StringComparison.Ordinal))
        || relative.StartsWith("tools/FourLevelDither/", StringComparison.Ordinal)
        || relative.StartsWith("tools/BluRayByteProbe/", StringComparison.Ordinal)
        || relative.StartsWith("tools/DvdGrayProbe/", StringComparison.Ordinal)
        || relative.StartsWith("src/DvdImageSolver/", StringComparison.Ordinal))
    {
        foreach (BaseObjectCreationExpressionSyntax creation in syntax.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
        {
            string type = creation is ObjectCreationExpressionSyntax explicitCreation ? explicitCreation.Type.ToString().Split('.').Last()
                : creation.Parent is ArrowExpressionClauseSyntax { Parent: MethodDeclarationSyntax method } ? method.ReturnType.ToString() : "";
            if (!type.EndsWith("Exception", StringComparison.Ordinal) || creation.ArgumentList is not { } arguments) continue;
            int index = type == "ArgumentOutOfRangeException" ? (arguments.Arguments.Count >= 3 ? 2 : 1)
                : type == "ArgumentNullException" ? 1 : 0;
            ExpressionSyntax? message = (arguments.Arguments.FirstOrDefault(argument => argument.NameColon?.Name.Identifier.ValueText == "message")
                                        ?? arguments.Arguments.ElementAtOrDefault(index))?.Expression;
            if (message is null) continue;
            foreach (ExpressionSyntax text in message.DescendantNodesAndSelf().OfType<ExpressionSyntax>()
                         .Where(node => node is InterpolatedStringExpressionSyntax || node.IsKind(SyntaxKind.StringLiteralExpression)))
            {
                Require(text.Ancestors().OfType<InvocationExpressionSyntax>()
                        .Any(call => call.Expression.ToString() is "L.T" or "L.F" or "L.Format"),
                    $"Unlocalized exception message in {relative}: {text}");
            }
            Require(message is not IdentifierNameSyntax { Identifier.ValueText: "PendingMappingMessage" },
                "Pending mapping guidance bypasses localization.");
        }
    }
    foreach (InvocationExpressionSyntax call in syntax.DescendantNodes().OfType<InvocationExpressionSyntax>())
    {
        if (relative == "src/DvdImageSolver/SelfTests.cs"
            && call.Expression.ToString() is "True" or "Equal" or "Near" or "SequenceEqual")
        {
            foreach (ExpressionSyntax text in call.ArgumentList.Arguments.Last().Expression.DescendantNodesAndSelf()
                         .OfType<ExpressionSyntax>().Where(node => node is InterpolatedStringExpressionSyntax || node.IsKind(SyntaxKind.StringLiteralExpression)))
                Require(text.Ancestors().OfType<InvocationExpressionSyntax>()
                    .Any(parent => parent.Expression.ToString() is "L.T" or "L.F" or "L.Format"),
                    "Unlocalized DVD selftest diagnostic: " + text);
        }
        int argumentIndex = call.Expression.ToString() switch
        {
            "L.T" or "L.F" or "L.Format" or "SetBurnStatus" => 0,
            "PublishPreview" => 2,
            "InvalidValue" when relative == "src/DiscImageStudio.Core/DiscPresetJsonStore.cs" => 2,
            "Failed" when relative == "src/DiscImageStudio.Core/Calibration/CalibrationPhotoRectifier.cs" => 0,
            "Text" when relative == "src/DiscImageStudio.Localization/CommandHelp.cs" => 0,
            "CatalogError" when relative == "src/DiscImageStudio.Localization/L.cs" => 0,
            "Option" when relative == "src/DiscImageStudio.Localization/CommandHelp.cs" => 1,
            _ => -1,
        };
        if (argumentIndex < 0) continue;
        ExpressionSyntax? expression = call.ArgumentList.Arguments.ElementAtOrDefault(argumentIndex)?.Expression;
        foreach (string key in ResourceKeys(expression)) CheckKey(key, Path.GetRelativePath(root, file));
    }
    static IEnumerable<string> ResourceKeys(ExpressionSyntax? expression)
    {
        switch (expression)
        {
            case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression):
                yield return literal.Token.ValueText;
                break;
            case InterpolatedStringExpressionSyntax interpolated:
                yield return FormatKey(interpolated);
                break;
            case ConditionalExpressionSyntax conditional:
                foreach (string key in ResourceKeys(conditional.WhenTrue).Concat(ResourceKeys(conditional.WhenFalse))) yield return key;
                break;
        }
    }
    foreach (VariableDeclaratorSyntax variable in syntax.DescendantNodes().OfType<VariableDeclaratorSyntax>())
    {
        if (variable.Identifier.ValueText is not ("Stages" or "_resultTitleKey" or "_burnStatusKey" or "PendingMappingMessage")) continue;
        if (variable.Initializer is null) continue;
        foreach (LiteralExpressionSyntax literal in variable.Initializer.DescendantNodes().OfType<LiteralExpressionSyntax>())
            if (literal.IsKind(SyntaxKind.StringLiteralExpression)) CheckKey(literal.Token.ValueText, file);
    }
}

XNamespace loc = "clr-namespace:DiscImageStudio.Localization";
foreach (string file in new[] { app, Path.Combine(root, "tools", "FourLevelDither") }
             .SelectMany(directory => Directory.GetFiles(directory, "*.xaml", SearchOption.AllDirectories)).Where(IsSourceFile))
{
    foreach (XElement element in XDocument.Load(file).Descendants())
    {
        foreach (XAttribute attribute in element.Attributes())
        {
            // Product names, language endonyms, and physical coordinate units are invariant.
            string[] invariantUiText = ["Disc Image Studio", "English", "日本語", "Türkçe", "X (mm)", "Y (mm)", " mm"];
            if (attribute.Name.LocalName is "Title" or "Text" or "Content" or "ToolTip" or "Header"
                or "Label" or "Hint" or "Placeholder" or "Description" or "ActionLabel" or "Badge"
                or "Subtitle" or "StatusText" or "Details" or "Note" or "ViewDescription" or "SelectionDescription"
                && attribute.Value.Any(char.IsLetter) && !invariantUiText.Contains(attribute.Value))
                Require(attribute.Value.StartsWith("{", StringComparison.Ordinal), $"Unlocalized UI text in {file}: {attribute}");
            if (attribute.Value.StartsWith("{loc:Text ", StringComparison.Ordinal))
                CheckKey(attribute.Value[10..^1], file);
            if (attribute.Name == loc + "LocalizedText.Key") CheckKey(attribute.Value, file);
        }
        if (element.Name.LocalName == "Setter" && (string?)element.Attribute("Property") == "loc:LocalizedText.Key")
            CheckKey((string)element.Attribute("Value")!, file);
    }
}

using (JsonDocument presets = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "src", "DiscImageStudio.Core", "Presets", "built-in-disc-presets.json"))))
{
    foreach (string group in new[] { "cdPresets", "dvdPresets", "bluRayPresets" })
        foreach (JsonElement preset in presets.RootElement.GetProperty(group).EnumerateArray())
            foreach (string field in new[] { "displayName", "description" })
                CheckKey(preset.GetProperty(field).GetString()!, $"{group}/{preset.GetProperty("id")}");
}

// These raw Core messages reach FitAssessment through a dynamic L.T lookup. Core keeps
// returning its original diagnostics; only the presentation translates them.
string fitter = Path.Combine(root, "src", "DiscImageStudio.Core", "Calibration", "CalibrationCrossFitter.cs");
foreach (LiteralExpressionSyntax literal in CSharpSyntaxTree.ParseText(File.ReadAllText(fitter)).GetRoot()
             .DescendantNodes().OfType<LiteralExpressionSyntax>())
{
    if (literal.IsKind(SyntaxKind.StringLiteralExpression) && literal.Token.ValueText.Any(c => c is >= '\u4e00' and <= '\u9fff')
        && !literal.Ancestors().Any(node => node is ThrowStatementSyntax or ThrowExpressionSyntax))
        CheckKey(literal.Token.ValueText, "calibration fit guidance");
}

foreach (string name in new[] { "CalibrationFitter.cs", "CalibrationCubicFitter.cs" })
{
    string file = Path.Combine(root, "src", "DiscImageStudio.Core", "Calibration", name);
    foreach (InvocationExpressionSyntax call in CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot()
                 .DescendantNodes().OfType<InvocationExpressionSyntax>())
    {
        // Classification prefixes in StartsWith remain raw. Only diagnostics
        // returned for presentation must have complete translations.
        if (call.Expression.ToString() is not ("warnings.Add" or "Failed")) continue;
        if (call.ArgumentList.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax literal
            && literal.IsKind(SyntaxKind.StringLiteralExpression)) CheckKey(literal.Token.ValueText, name);
    }
}

CultureInfo originalCulture = CultureInfo.CurrentCulture;
try
{
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
    string? originalEnvironment = Environment.GetEnvironmentVariable(CommandLineLanguage.EnvironmentVariable);
    try
    {
        Environment.SetEnvironmentVariable(CommandLineLanguage.EnvironmentVariable, "en");
        string[] remaining = CommandLineLanguage.Initialize(
            ["ui-snapshot", "--dock", "--language", "system", "--output", "preview.png"], ["--dock"]);
        Require(remaining.SequenceEqual(new[] { "ui-snapshot", "--dock", "--output", "preview.png" }),
            "Global language parsing changed snapshot flags or values.");
        Require(L.PreferredLanguage == L.SystemLanguage && L.CurrentLanguage == L.ResolveLanguage(L.SystemLanguage),
            "Following the system lost its settings selection when resolving the rendering language.");
        remaining = CommandLineLanguage.Initialize(
            ["cd-generate", "--input", "--language", "--output", "--language=ja", "--language", "tr"]);
        Require(remaining.SequenceEqual(new[] { "cd-generate", "--input", "--language", "--output", "--language=ja" })
            && L.CurrentLanguage == L.Turkish, "An option value was interpreted as a global flag.");
        Require(CultureInfo.CurrentCulture.Name == "tr-TR", "CLI language selection changed numeric culture.");
    }
    finally
    {
        Environment.SetEnvironmentVariable(CommandLineLanguage.EnvironmentVariable, originalEnvironment);
    }
    string fallback = PresetText.UserDescriptionFallback(24.5, 56.8, 359849);
    foreach (string language in languages)
    {
        L.SetLanguage(language);
        Require(CultureInfo.CurrentCulture.Name == "tr-TR", "Language switch changed numeric culture.");
        string expected = L.Format(PresetText.UserDescriptionKey, 24.5, 56.8, 359849L);
        Require(PresetText.Description(fallback, PresetText.UserDescriptionKey, null, 24.5, 56.8, 359849) == expected,
            $"Generated preset description not translated: {language}");
        Require(PresetText.Description(fallback, null, null, 24.5, 56.8, 359849) == expected,
            $"Legacy generated preset description not translated: {language}");
        Require(PresetText.Description("My notes", PresetText.UserDescriptionKey, null, 24.5, 56.8, 359849) == "My notes",
            "User-edited description was translated.");
        Require(PresetText.Resolve("My preset", null, "标准 80min CD") == "My preset", "User name was translated.");
        Require(PresetText.Resolve("Fallback", "unknown-resource", null) == "Fallback", "Missing resource lost its fallback.");
        Require(PresetText.Resolve("Fallback", "Xaml.Cancel", null) == L.T("Xaml.Cancel"), "Explicit resource key was ignored.");
    }
}
finally
{
    CultureInfo.CurrentCulture = originalCulture;
}

BluRayEncodingTests.Run();
ModuleTranslationTests.Run(root, languages);
DescriptorTranslationTests.Run(languages);
ValidationTranslationTests.Run(languages);
DitherToolTranslationTests.Run(languages);
CommandHelpTranslationTests.Run(languages);
DvdTranslationTests.Run(languages);
InitialPresetTranslationTests.Run(languages, CheckKey);
CatalogLoadingTests.Run(root, languages);
Console.WriteLine($"Localization checks passed: {L.Entries.Count} entries × {languages.Length} languages; {references} source references, file filters, formats, and preset fallbacks.");

static bool IsSourceFile(string path) => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "obj" or "bin");

static string FormatKey(InterpolatedStringExpressionSyntax expression)
{
    int index = 0;
    return string.Concat(expression.Contents.Select(part => part switch
    {
        InterpolatedStringTextSyntax text => text.TextToken.ValueText,
        InterpolationSyntax argument => "{" + index++
            + (argument.AlignmentClause is { } alignment ? "," + alignment.Value : "")
            + (argument.FormatClause is { } format ? ":" + format.FormatStringToken.ValueText : "") + "}",
        _ => throw new InvalidOperationException("Unexpected interpolated string content."),
    }));
}

static HashSet<string> ReadArguments(string text)
{
    CompositeFormat format = CompositeFormat.Parse(text);
    HashSet<string> seen = new(StringComparer.Ordinal);
    object[] arguments = Enumerable.Range(0, format.MinimumArgumentCount).Select(i => (object)new RecordingArgument(i, seen)).ToArray();
    _ = string.Format(CultureInfo.InvariantCulture, format, arguments);
    return seen;
}

sealed record RecordingArgument(int Index, HashSet<string> Seen) : IFormattable
{
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        Seen.Add(Index + ":" + format);
        return string.Empty;
    }
}
