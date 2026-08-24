using TimeLockApp.Services;

internal static class SessionExpiryCountdownTests
{
    public static IEnumerable<(string Name, Action Run)> All()
    {
        yield return ("expiry countdown logs out on sixtieth tick", LogsOutOnSixtiethTick);
        yield return ("expiry countdown emits one terminal action", EmitsOneTerminalAction);
        yield return ("extension attempt does not extend expiry deadline", ExtensionAttemptDoesNotExtendDeadline);
        yield return ("late extension completion cannot beat expiry deadline", LateExtensionCannotComplete);
    }

    private static void LogsOutOnSixtiethTick()
    {
        TimeSpan elapsed = TimeSpan.Zero;
        SessionExpiryCountdown countdown = new(60, () => elapsed);

        elapsed = TimeSpan.FromSeconds(59);
        AssertTrue(countdown.Tick() == SessionExpiryAction.None, "Countdown must not end early.");

        AssertTrue(countdown.RemainingSeconds == 1, "One second must remain before the terminal tick.");
        elapsed = TimeSpan.FromSeconds(60);
        AssertTrue(countdown.Tick() == SessionExpiryAction.Logout, "The sixtieth tick must force logout.");
    }

    private static void EmitsOneTerminalAction()
    {
        SessionExpiryCountdown countdown = new(1);

        AssertTrue(countdown.SelectLogout() == SessionExpiryAction.Logout, "Explicit logout must win.");
        AssertTrue(countdown.Tick() == SessionExpiryAction.None, "A later timer tick must not emit another action.");
        AssertTrue(countdown.SelectLogout() == SessionExpiryAction.None, "Logout must be emitted once.");
    }

    private static void ExtensionAttemptDoesNotExtendDeadline()
    {
        TimeSpan elapsed = TimeSpan.Zero;
        SessionExpiryCountdown countdown = new(60, () => elapsed);

        AssertTrue(countdown.TryBeginExtension(), "First extension click must start an attempt.");
        elapsed = TimeSpan.FromSeconds(60);

        AssertTrue(countdown.Tick() == SessionExpiryAction.Logout, "Pending confirmation must not extend the deadline.");
        AssertTrue(countdown.RemainingSeconds == 0, "Deadline must be monotonic and authoritative.");
    }

    private static void LateExtensionCannotComplete()
    {
        TimeSpan elapsed = TimeSpan.Zero;
        SessionExpiryCountdown countdown = new(60, () => elapsed);
        AssertTrue(countdown.TryBeginExtension(), "Extension attempt must start before the deadline.");

        elapsed = TimeSpan.FromSeconds(60);

        AssertTrue(!countdown.TryCompleteExtension(), "A response at or after the deadline must not extend the session.");
        AssertTrue(countdown.Tick() == SessionExpiryAction.Logout, "Expired completion must resolve to logout.");
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
