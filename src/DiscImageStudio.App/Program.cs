using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DiscImageStudio;

internal static class Program
{
    [STAThread]
    private static int Main(string[] arguments)
    {
        if (arguments.Length == 0)
        {
            return AppHost.Run();
        }

        AttachToParentConsole();
        return UnifiedCommandRunner.Run(arguments);
    }

    private static void AttachToParentConsole()
    {
        if (!OperatingSystem.IsWindows() || !AttachConsole(AttachParentProcess))
        {
            return;
        }

        Console.OutputEncoding = new UTF8Encoding(false);
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
    }

    private const uint AttachParentProcess = 0xFFFFFFFF;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(uint processId);
}
