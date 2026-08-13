using TimeLockApp.Services;

static class WebView2ProfilePathTests
{
    public static IEnumerable<(string Name, Action Run)> All()
    {
        yield return ("uses a writable per-user WebView2 profile", UsesAWritablePerUserWebView2Profile);
    }

    private static void UsesAWritablePerUserWebView2Profile()
    {
        string profilePath = WebView2ProfilePath.GetUserDataFolder();
        string expectedRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TimeLockApp",
            "WebView2");

        AssertTrue(
            string.Equals(profilePath, expectedRoot, StringComparison.OrdinalIgnoreCase),
            $"WebView2 profile should be stored under '{expectedRoot}', but was '{profilePath}'.");
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
