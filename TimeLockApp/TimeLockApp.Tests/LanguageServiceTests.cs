using TimeLockApp.Services;

internal static class LanguageServiceTests
{
    public static IEnumerable<(string Name, Action Run)> All()
    {
        yield return ("language service starts in Thai", StartsInThai);
        yield return ("language service switches to English", SwitchesToEnglish);
        yield return ("language service formats placeholders", FormatsPlaceholders);
        yield return ("language service falls back to key", FallsBackToKey);
    }

    private static void StartsInThai()
    {
        LanguageService service = new();

        AssertEqual("เข้าสู่ระบบ", service.Get("LoginButton"));
        AssertEqual("th", service.CurrentLanguage);
    }

    private static void SwitchesToEnglish()
    {
        LanguageService service = new();

        service.SetLanguage("en");

        AssertEqual("Log in", service.Get("LoginButton"));
        AssertEqual("en", service.CurrentLanguage);
    }

    private static void FormatsPlaceholders()
    {
        LanguageService service = new();

        service.SetLanguage("en");

        AssertEqual(
            "Sync failed: network unavailable",
            service.Get("SyncFailed", "network unavailable"));
    }

    private static void FallsBackToKey()
    {
        LanguageService service = new();

        AssertEqual("MissingKey", service.Get("MissingKey"));
    }

    private static void AssertEqual(string expected, string actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Expected '{expected}', got '{actual}'.");
        }
    }
}
