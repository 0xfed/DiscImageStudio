using System.Globalization;
using System.Diagnostics;
using System.Text;
using DiscImageStudio.Localization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

internal static class CatalogLoadingTests
{
    public static void Run(string root, string[] languages)
    {
        CultureInfo numeric = CultureInfo.CurrentCulture;
        foreach (string language in languages)
        {
            L.Initialize(language);
            string name = "resource-日本語-İ.json";
            using MemoryStream empty = new(Encoding.UTF8.GetBytes("null"));
            Expect(() => L.LoadCatalog([(name, empty)]), L.Format("Empty localization catalog: {0}", name));
            if (empty.CanRead) throw new InvalidOperationException("Empty catalog stream was not disposed.");

            string key = "key-日本語-İ";
            byte[] json = Encoding.UTF8.GetBytes("{\"" + key + "\":{\"en\":\"value\"}}");
            using MemoryStream first = new(json);
            using MemoryStream second = new(json);
            Expect(() => L.LoadCatalog([("first.json", first), ("second.json", second)]),
                L.Format("Duplicate localization key: {0}", key));
            if (first.CanRead || second.CanRead)
                throw new InvalidOperationException("Duplicate catalog streams were not disposed.");
            if (!Equals(CultureInfo.CurrentCulture, numeric))
                throw new InvalidOperationException("Catalog diagnostics changed numeric culture.");
        }
        CheckStartupFailure(root, languages);
        Console.WriteLine("catalog loading: passed (five-language startup failures, preserved names and numeric culture, disposed streams)");
    }

    private static void CheckStartupFailure(string root, string[] languages)
    {
        // Compile the actual runtime with a corrupt resource, then start a fresh
        // process per language. This exercises Lazy failure and the same error
        // wrapper as the CLI without altering the application's valid catalogs.
        const string broken = "DiscImageStudio.Localization.Catalogs.Broken.json";
        string fixture = Path.Combine(Path.GetTempPath(), "disc-catalog-startup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        try
        {
            string directory = Path.Combine(root, "src", "DiscImageStudio.Localization");
            string[] sources = [
                "global using System; global using System.IO; global using System.Linq; global using System.Collections.Generic;",
                File.ReadAllText(Path.Combine(directory, "L.cs")),
                File.ReadAllText(Path.Combine(directory, "DiagnosticMessage.cs")),
                File.ReadAllText(Path.Combine(directory, "CommandLineLanguage.cs")),
                """
                using DiscImageStudio.Localization;
                using System.Globalization;
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                try
                {
                    CommandLineLanguage.Initialize(args);
                    Console.WriteLine(L.T("trigger-main-catalog"));
                    return 99;
                }
                catch (Exception error)
                {
                    Console.Error.WriteLine(L.Format("Cli.Error", error.Message));
                    return CultureInfo.CurrentCulture.Name == "fr-FR" ? 1 : 98;
                }
                """];
            string[] assemblies = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator);
            var compilation = CSharpCompilation.Create("CatalogStartupFixture", sources.Select(source => CSharpSyntaxTree.ParseText(source)),
                assemblies.Where(path => Path.GetFileName(path) != "DiscImageStudio.Localization.dll")
                    .Select(path => MetadataReference.CreateFromFile(path)),
                new CSharpCompilationOptions(OutputKind.ConsoleApplication, nullableContextOptions: NullableContextOptions.Enable));
            byte[] errors = File.ReadAllBytes(Path.Combine(directory, "Catalogs", "CatalogLoader.json"));
            ResourceDescription[] resources = [
                new("DiscImageStudio.Localization.Catalogs.CatalogLoader.json", () => new MemoryStream(errors), true),
                new(broken, () => new MemoryStream(Encoding.UTF8.GetBytes("null")), true)];
            string dll = Path.Combine(fixture, "fixture.dll");
            using (FileStream output = File.Create(dll))
            {
                var result = compilation.Emit(output, manifestResources: resources);
                if (!result.Success)
                    throw new InvalidOperationException("Startup fixture compilation failed: " + string.Join("\n", result.Diagnostics));
            }
            File.Copy(Path.ChangeExtension(typeof(CatalogLoadingTests).Assembly.Location, ".runtimeconfig.json"),
                Path.Combine(fixture, "fixture.runtimeconfig.json"));
            foreach (string language in languages)
            {
                L.Initialize(language);
                ProcessStartInfo start = new("dotnet")
                {
                    RedirectStandardError = true, RedirectStandardOutput = true,
                    StandardErrorEncoding = Encoding.UTF8, StandardOutputEncoding = Encoding.UTF8,
                };
                start.ArgumentList.Add(dll);
                start.ArgumentList.Add("--language");
                start.ArgumentList.Add(language);
                start.Environment[CommandLineLanguage.EnvironmentVariable] = "en";
                using Process process = Process.Start(start)!;
                Task<string> error = process.StandardError.ReadToEndAsync();
                Task<string> output = process.StandardOutput.ReadToEndAsync();
                if (!process.WaitForExit(20000))
                {
                    process.Kill(entireProcessTree: true);
                    throw new InvalidOperationException("Startup failure fixture timed out.");
                }
                string expected = L.Format("Cli.Error", L.Format("Empty localization catalog: {0}", broken));
                if (process.ExitCode != 1 || error.GetAwaiter().GetResult().TrimEnd() != expected
                    || output.GetAwaiter().GetResult().Length != 0)
                    throw new InvalidOperationException("Corrupt startup catalog lost the translated CLI diagnostic: " + language);
            }
        }
        finally { Directory.Delete(fixture, recursive: true); }
    }

    private static void Expect(Action action, string expected)
    {
        try { action(); }
        catch (InvalidDataException error) when (error.Message == expected) { return; }
        throw new InvalidOperationException("Catalog failure lost its type or localized message.");
    }
}
