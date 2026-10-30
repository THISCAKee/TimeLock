using TimeLockApp.Services;

internal static class LanguageServiceTests
{
    public static IEnumerable<(string Name, Action Run)> All()
    {
        yield return ("language service starts in Thai", StartsInThai);
        yield return ("language service switches to English", SwitchesToEnglish);
        yield return ("language service formats placeholders", FormatsPlaceholders);
        yield return ("language service falls back to key", FallsBackToKey);
        yield return ("session extension messages exist in Thai and English", SessionExtensionMessagesExist);
        yield return ("usage guide messages exist in Thai and English", UsageGuideMessagesExist);
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

    private static void SessionExtensionMessagesExist()
    {
        string[] keys =
        [
            "SessionExpiredHeading",
            "SessionExpiredCountdown",
            "SessionExpiredNextBooking",
            "ExtendThreeHours",
            "ExtensionChecking",
            "ExtensionRetry",
            "ExtensionLimitReached",
            "ExtensionCrossesMidnight",
            "ExtensionNextBookingConflict",
            "ExtensionBookingInactive",
            "ExtensionAccountMismatch",
            "ExtensionUnavailable"
        ];
        LanguageService service = new();

        foreach (string key in keys)
        {
            string thai = service.Get(key);
            AssertNotFallback(key, thai, "Thai");
        }
        AssertEqual("ต่อเวลา 3 ชั่วโมง", service.Get("ExtendThreeHours"));
        AssertEqual(
            "หมดเวลาใช้งานและมีคิวจองถัดไป จึงไม่สามารถต่อเวลาได้",
            service.Get("SessionExpiredNextBooking"));

        service.SetLanguage("en");
        foreach (string key in keys)
        {
            string english = service.Get(key);
            AssertNotFallback(key, english, "English");
        }
        AssertEqual("Extend 3 hours", service.Get("ExtendThreeHours"));
        AssertEqual(
            "Your usage time has expired and another booking is scheduled, so you cannot extend this session.",
            service.Get("SessionExpiredNextBooking"));
    }

    private static void UsageGuideMessagesExist()
    {
        string[] keys =
        [
            "UsageGuideTitle",
            "UsageGuideIntro",
            "UsageGuideBeforeTitle",
            "UsageGuideBeforeBody",
            "UsageGuideLoginTitle",
            "UsageGuideLoginBody",
            "UsageGuideInternetTitle",
            "UsageGuideInternetBody",
            "UsageGuideDuringTitle",
            "UsageGuideDuringBody",
            "UsageGuideFinishTitle",
            "UsageGuideFinishBody",
            "UsageGuideHelpTitle",
            "UsageGuideHelpBody",
            "UsageGuideFooter"
        ];
        LanguageService service = new();

        foreach (string key in keys)
        {
            AssertNotFallback(key, service.Get(key), "Thai");
        }

        service.SetLanguage("en");

        foreach (string key in keys)
        {
            AssertNotFallback(key, service.Get(key), "English");
        }
    }

    private static void AssertNotFallback(string key, string actual, string language)
    {
        if (string.IsNullOrWhiteSpace(actual) || actual == key)
        {
            throw new InvalidOperationException($"{language} value is missing for '{key}'.");
        }
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
