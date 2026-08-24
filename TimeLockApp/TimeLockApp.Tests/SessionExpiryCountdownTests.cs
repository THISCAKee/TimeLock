using TimeLockApp.Services;

internal static class SessionExpiryCountdownTests
{
    public static IEnumerable<(string Name, Action Run)> All()
    {
        yield return ("expiry countdown logs out on sixtieth tick", LogsOutOnSixtiethTick);
        yield return ("expiry countdown emits one terminal action", EmitsOneTerminalAction);
        yield return ("extension attempt pauses and retry resumes countdown", ExtensionAttemptPausesAndRetryResumes);
    }

    private static void LogsOutOnSixtiethTick()
    {
        SessionExpiryCountdown countdown = new(60);

        for (int second = 1; second < 60; second++)
        {
            AssertTrue(countdown.Tick() == SessionExpiryAction.None, "Countdown must not end early.");
        }

        AssertTrue(countdown.RemainingSeconds == 1, "One second must remain before the terminal tick.");
        AssertTrue(countdown.Tick() == SessionExpiryAction.Logout, "The sixtieth tick must force logout.");
    }

    private static void EmitsOneTerminalAction()
    {
        SessionExpiryCountdown countdown = new(1);

        AssertTrue(countdown.SelectLogout() == SessionExpiryAction.Logout, "Explicit logout must win.");
        AssertTrue(countdown.Tick() == SessionExpiryAction.None, "A later timer tick must not emit another action.");
        AssertTrue(countdown.SelectLogout() == SessionExpiryAction.None, "Logout must be emitted once.");
    }

    private static void ExtensionAttemptPausesAndRetryResumes()
    {
        SessionExpiryCountdown countdown = new(60);
        countdown.Tick();

        AssertTrue(countdown.TryBeginExtension(), "First extension click must start an attempt.");
        AssertTrue(countdown.Tick() == SessionExpiryAction.None, "Timer must pause while confirmation is pending.");
        AssertTrue(countdown.RemainingSeconds == 59, "Pending confirmation must not consume popup time.");
        countdown.AllowExtensionRetry();
        AssertTrue(countdown.Tick() == SessionExpiryAction.None, "Countdown must resume after a retryable failure.");
        AssertTrue(countdown.RemainingSeconds == 58, "Resumed countdown must continue from its prior value.");
        AssertTrue(countdown.TryBeginExtension(), "Retry must be selectable again.");
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
