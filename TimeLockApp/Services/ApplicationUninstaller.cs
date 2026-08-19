using System.Diagnostics;
using System.IO;

namespace TimeLockApp.Services;

public static class ApplicationUninstaller
{
    public static string? FindUninstaller(string applicationDirectory)
    {
        string uninstallerPath = Path.Combine(
            applicationDirectory,
            ".uninstall",
            "unins000.exe");

        return File.Exists(uninstallerPath)
            ? uninstallerPath
            : null;
    }

    public static bool TryStart(
        string applicationDirectory,
        out string errorMessage)
    {
        string? uninstallerPath = FindUninstaller(applicationDirectory);

        if (uninstallerPath is null)
        {
            errorMessage = "Uninstaller was not found.";
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uninstallerPath,
                UseShellExecute = true,
                Verb = "runas"
            });

            errorMessage = string.Empty;
            return true;
        }
        catch (Exception ex) when (
            ex is InvalidOperationException ||
            ex is System.ComponentModel.Win32Exception)
        {
            errorMessage = ex.Message;
            return false;
        }
    }
}
