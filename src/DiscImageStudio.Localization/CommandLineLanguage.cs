namespace DiscImageStudio.Localization;

/// <summary>Shared global language selection for all console entry points.
/// Command names, option values, and numeric culture are never translated.</summary>
public static class CommandLineLanguage
{
    public const string EnvironmentVariable = "DISC_IMAGE_STUDIO_LANGUAGE";

    public static string[] Initialize(string[] arguments, IEnumerable<string>? valuelessOptions = null)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        L.Initialize(Environment.GetEnvironmentVariable(EnvironmentVariable));
        HashSet<string> switches = new(valuelessOptions ?? [], StringComparer.OrdinalIgnoreCase)
        {
            "--help", "-h",
        };
        List<string> remaining = [];
        string? selected = null;
        bool specified = false;
        for (int index = 0; index < arguments.Length; index++)
        {
            string argument = arguments[index];
            if (argument == "--")
            {
                remaining.AddRange(arguments.Skip(index));
                break;
            }
            bool inline = argument.StartsWith("--language=", StringComparison.OrdinalIgnoreCase);
            if (!inline && !argument.Equals("--language", StringComparison.OrdinalIgnoreCase))
            {
                remaining.Add(argument);
                // Module options take a value. Do not reinterpret that value as a
                // global flag: a perfectly valid input filename may be "--language".
                if (argument.StartsWith("--", StringComparison.Ordinal)
                    && !argument.Contains('=') && !switches.Contains(argument)
                    && index + 1 < arguments.Length)
                    remaining.Add(arguments[++index]);
                continue;
            }

            if (specified) throw new ArgumentException(L.T("Cli.DuplicateLanguage"));
            specified = true;
            if (inline) selected = argument[11..];
            else if (++index < arguments.Length && !arguments[index].StartsWith("--", StringComparison.Ordinal))
                selected = arguments[index];
            else throw new ArgumentException(L.T("Cli.MissingLanguage"));

            string normalized = L.NormalizeLanguage(selected);
            if (string.IsNullOrWhiteSpace(selected)
                || (normalized == L.SystemLanguage && !selected.Trim().Equals(L.SystemLanguage, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException(L.Format("Cli.UnsupportedLanguage", selected));
            L.Initialize(normalized);
        }

        return remaining.ToArray();
    }
}
