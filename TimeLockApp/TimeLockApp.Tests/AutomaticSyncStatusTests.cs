using TimeLockApp.Services;

internal static class AutomaticSyncStatusTests
{
    public static IEnumerable<(string Name, Action Run)> All()
    {
        yield return (
            "automatic sync success status includes time and count",
            SuccessIncludesTimeAndCount);
        yield return (
            "automatic sync failure status includes error and retry",
            FailureIncludesErrorAndRetry);
    }

    private static void SuccessIncludesTimeAndCount()
    {
        string status = AutomaticSyncStatus.Format(
            UserSyncResult.Success(4, hasChanges: true),
            new DateTime(2026, 8, 7, 16, 5, 0));

        AssertContains(status, "16:05:00");
        AssertContains(status, "4");
    }

    private static void FailureIncludesErrorAndRetry()
    {
        string status = AutomaticSyncStatus.Format(
            UserSyncResult.Failure("network unavailable"),
            new DateTime(2026, 8, 7, 16, 6, 0));

        AssertContains(status, "network unavailable");
        AssertContains(status, "ลองใหม่");
    }

    private static void AssertContains(string actual, string expected)
    {
        if (!actual.Contains(expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Expected '{actual}' to contain '{expected}'.");
        }
    }
}
