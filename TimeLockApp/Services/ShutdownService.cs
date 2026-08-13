using System.Diagnostics;
using System.IO;

namespace TimeLockApp.Services;

public static class ShutdownService
{
    public static ProcessStartInfo CreateStartInfo()
    {
        return new ProcessStartInfo
        {
            FileName = Path.Combine(
                Environment.SystemDirectory,
                "shutdown.exe"),
            Arguments = "/s /f /t 0",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Environment.SystemDirectory
        };
    }

    public static void Shutdown()
    {
        Process.Start(CreateStartInfo());
    }
}
