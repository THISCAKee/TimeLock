using System;
using System.IO;

namespace TimeLockApp.Services;

public static class WebView2ProfilePath
{
    public static string GetUserDataFolder()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TimeLockApp",
            "WebView2");
    }
}
