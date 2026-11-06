using DiscImageStudio.Burning;
using DiscImageStudio.Localization;
using DiscImageStudio.Core;
using DiscImageStudio.ViewModels;

namespace DiscImageStudio;

internal static class UnifiedCommandRunner
{
    private static readonly string[] SnapshotSwitches =
    [
        "--settings-dialog", "--preset-dialog", "--dock", "--expand", "--home-hover",
        "--project-update", "--project-save", "--tone-preview",
    ];

    private static DiscModuleCatalog Modules => ConsoleModuleRunner.Catalog;

    internal static int Run(string[] arguments)
    {
        try
        {
            arguments = CommandLineLanguage.Initialize(arguments, SnapshotSwitches);
            if (arguments.Length == 0)
            {
                throw new ArgumentException(L.T("Missing command."));
            }

            string command = arguments[0].ToLowerInvariant();
            if (command is "help" or "--help" or "-h")
            {
                return PrintHelp();
            }

            if (command == "ui-snapshot")
            {
                ReadOnlySpan<string> snapshotArguments = arguments.AsSpan(1);
                string? tabValue = FindOption(snapshotArguments, "tab");
                int workspaceIndex = tabValue is null ? 0 : int.Parse(tabValue);
                if (workspaceIndex < 0 || workspaceIndex > (int)Workspace.CalibrationBurn)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(arguments),
                        L.T("Tabs: 0 home, 1 design, 2 preview, 3 export, 4 burn, 5 calibration, 6 calibration burn."));
                }

                bool settingsDialog = HasOption(snapshotArguments, "settings-dialog");
                return UiPreviewRenderer.Render(
                    RequireOption(snapshotArguments, "output"),
                    (Workspace)workspaceIndex,
                    FindOption(snapshotArguments, "preview"),
                    FindOption(snapshotArguments, "live-input"),
                    FindOption(snapshotArguments, "live-disc"),
                    FindOption(snapshotArguments, "live-mode"),
                    int.Parse(FindOption(snapshotArguments, "width") ?? (settingsDialog ? "760" : "1366")),
                    int.Parse(FindOption(snapshotArguments, "height") ?? (settingsDialog ? "600" : "768")),
                    FindOption(snapshotArguments, "calibration-session"),
                    FindOption(snapshotArguments, "calibration-view"),
                    FindOption(snapshotArguments, "project"),
                    FindOption(snapshotArguments, "project-dialog"),
                    HasOption(snapshotArguments, "preset-dialog"),
                    HasOption(snapshotArguments, "dock"),
                    FindOption(snapshotArguments, "burn-source"),
                    HasOption(snapshotArguments, "expand"),
                    int.Parse(FindOption(snapshotArguments, "scroll") ?? "0"),
                    HasOption(snapshotArguments, "home-hover"),
                    HasOption(snapshotArguments, "project-update"),
                    HasOption(snapshotArguments, "project-save"),
                    HasOption(snapshotArguments, "tone-preview"),
                    FindOption(snapshotArguments, "burn-progress"),
                    FindOption(snapshotArguments, "theme"),
                    settingsDialog,
                    FindOption(snapshotArguments, "settings-view"),
                    L.PreferredLanguage);
            }

            if (command == "burn-devices")
            {
                return PrintBurnDevices();
            }

            if (command == "burn-build-info")
            {
                return PrintBurnBuildInfo();
            }

            return ConsoleModuleRunner.Run(arguments);
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine(L.T("Cli.Cancelled"));
            return 130;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(L.Format("Cli.Error", exception));
            return 1;
        }
    }

    private static int PrintHelp()
    {
        Console.WriteLine(CommandHelp.WindowsIntro);
        Console.WriteLine(L.T("Cli.LanguageHelp"));
        foreach (IOpticalDiscModule module in Modules.Modules)
        {
            Console.WriteLine($"\n{module.Descriptor.DisplayName} [{module.Descriptor.Id}]");
            foreach (DiscCommandDescriptor command in module.Descriptor.Commands)
            {
                Console.WriteLine($"  {command.Name,-20} {command.Description}");
            }
        }

        Console.WriteLine(CommandHelp.WindowsDetails);
        return 0;
    }

    private static int PrintBurnDevices()
    {
        IReadOnlyList<OpticalBurnDevice> devices = new WindowsImapiBurner()
            .GetDevicesAsync()
            .GetAwaiter()
            .GetResult();
        if (devices.Count == 0)
        {
            Console.WriteLine(L.T("No IMAPI2 optical recorder was found."));
            return 0;
        }

        foreach (OpticalBurnDevice device in devices)
        {
            Console.WriteLine($"{device.DisplayName}\t{device.Id}");
        }

        return 0;
    }

    private static int PrintBurnBuildInfo()
    {
        Type burningAssemblyMarker = typeof(WindowsImapiBurner);
        Type? sessionType = burningAssemblyMarker.Assembly.GetType(
            "DiscImageStudio.Burning.ICdTrackAtOnceSession",
            throwOnError: false);
        bool containsLegacyFinalizeSetter = sessionType?
            .GetProperty("DoNotFinalizeMedia") is not null;
        Type? rawSessionType = burningAssemblyMarker.Assembly.GetType(
            "DiscImageStudio.Burning.ICdRawSession",
            throwOnError: false);
        bool containsRawDaoWriter = rawSessionType?
            .GetMethod("WriteMedia") is not null;
        Console.WriteLine(
            L.F($"Burning assembly MVID: {burningAssemblyMarker.Module.ModuleVersionId}"));
        Console.WriteLine(
            L.F($"Legacy DoNotFinalizeMedia setter present: {containsLegacyFinalizeSetter}"));
        Console.WriteLine(L.F($"Raw CD DAO writer present: {containsRawDaoWriter}"));
        return containsLegacyFinalizeSetter || !containsRawDaoWriter ? 1 : 0;
    }

    private static string RequireOption(ReadOnlySpan<string> arguments, string key)
        => FindOption(arguments, key)
            ?? throw new ArgumentException(L.F($"Missing required option --{key}."));

    private static string? FindOption(ReadOnlySpan<string> arguments, string key)
    {
        string name = "--" + key;
        string prefix = name + "=";
        for (int index = 0; index < arguments.Length; index++)
        {
            string argument = arguments[index];
            if (argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return argument[prefix.Length..];
            }

            if (argument.Equals(name, StringComparison.OrdinalIgnoreCase)
                && index + 1 < arguments.Length)
            {
                return arguments[index + 1];
            }
        }

        return null;
    }

    /// Presence check for valueless flags (<c>--dock</c>): FindOption cannot express them —
    /// trailing, it finds no next token; mid-order, it would swallow the next flag as a value.
    private static bool HasOption(ReadOnlySpan<string> arguments, string key)
    {
        string name = "--" + key;
        for (int index = 0; index < arguments.Length; index++)
        {
            if (arguments[index].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

}
