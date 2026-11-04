using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using DiscImageStudio.Dvd;
using DiscImageStudio.Localization;

internal static class CommandHelpTranslationTests
{
    internal static void Run(string[] languages)
    {
        string original = L.PreferredLanguage;
        CultureInfo culture = CultureInfo.CurrentCulture;
        TextWriter output = Console.Out;
        MethodInfo run = typeof(DvdDiscModule).Assembly.GetType("DvdImageSolver.Program")!
            .GetMethod("RunCommand", BindingFlags.Static | BindingFlags.NonPublic)!;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            foreach (string language in languages)
            {
                L.SetLanguage(language);
                foreach ((CommandHelp.HelpLine[] rows, string text) in new[]
                {
                    (CommandHelp.DvdLines, CommandHelp.Dvd),
                    (CommandHelp.DvdGrayProbeLines, CommandHelp.DvdGrayProbe),
                    (CommandHelp.WindowsIntroLines, CommandHelp.WindowsIntro),
                    (CommandHelp.WindowsDetailsLines, CommandHelp.WindowsDetails),
                })
                {
                    string[] lines = text.Split(Environment.NewLine);
                    Check(lines.Length == rows.Length, "help line count in " + language);
                    for (int index = 0; index < rows.Length; index++)
                    {
                        CommandHelp.HelpLine row = rows[index];
                        Check(lines[index].StartsWith(row.Prefix, StringComparison.Ordinal), "command syntax changed");
                        if (row.Key is null) Check(lines[index] == row.Prefix, "literal command or formula changed");
                        else
                        {
                            Check(L.TryGetText(row.Key, out string translated), "unregistered help text: " + row.Key);
                            Check(lines[index][row.Prefix.Length..] == translated, "help did not follow " + language);
                            foreach (string token in Tokens(row.Key))
                                Check(translated.Contains(token, StringComparison.Ordinal), "help lost technical token in " + language + ": " + token);
                        }
                    }
                }
                Check(CommandHelp.WindowsDetails.Contains("cd|dvd|bd", StringComparison.Ordinal)
                      && CommandHelp.WindowsDetails.Contains("UDF 2.01", StringComparison.Ordinal),
                    "Windows help lost BD snapshots or the current filesystem description");
                using StringWriter captured = new();
                Console.SetOut(captured);
                int result = (int)run.Invoke(null, [new[] { "--help" }, false])!;
                Console.SetOut(output);
                Check(result == 0 && captured.ToString().Contains(CommandHelp.Dvd, StringComparison.Ordinal)
                      && captured.ToString().Contains(L.T("Cli.LanguageHelp"), StringComparison.Ordinal),
                    "actual DVD entry point bypassed translated help in " + language);
                Check(CultureInfo.CurrentCulture.Name == "tr-TR", "help changed numeric culture");
            }
            Console.WriteLine("command help translations: passed (Windows/DVD prose in five languages, preserved commands/formulas/default tokens, actual DVD help entry point)");
        }
        finally { Console.SetOut(output); L.Initialize(original); CultureInfo.CurrentCulture = culture; }
    }

    private static IEnumerable<string> Tokens(string source) =>
        Regex.Matches(source, @"--[a-z][a-z0-9*-]*|0x[0-9A-Fa-f]+|-?\d+\.\.-?\d+|\b(?:DVD_IMAGE|IMAGE\.iso\.json|finalBoundary|null|land|cw|random|false|true)\b")
            .Select(match => match.Value)
            .Where(token => token != "random" || source.Contains("default random", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
