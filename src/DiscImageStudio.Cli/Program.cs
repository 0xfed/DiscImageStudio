using System.Text;
using DiscImageStudio.Core;
using DiscImageStudio.Localization;

namespace DiscImageStudio.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            args = CommandLineLanguage.Initialize(args);
            if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
            {
                Console.WriteLine(L.T("Cli.Title"));
                Console.WriteLine(L.T("Cli.Usage"));
                Console.WriteLine(L.T("Cli.LanguageHelp"));
                foreach (IOpticalDiscModule module in ConsoleModuleRunner.Catalog.Modules)
                {
                    Console.WriteLine($"\n{module.Descriptor.DisplayName}");
                    foreach (DiscCommandDescriptor command in module.Descriptor.Commands)
                        Console.WriteLine($"  {command.Name,-20} {command.Description}");
                }
                return 0;
            }

            return ConsoleModuleRunner.Run(args);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine(L.T("Cli.Cancelled"));
            return 130;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(L.Format("Cli.Error", error.Message));
            return 1;
        }
    }
}
