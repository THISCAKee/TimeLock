using TimeLockApp.Services;

internal static class LockAndWarningTests
{
    public static IEnumerable<(string Name, Action Run)> All()
    {
        yield return ("login blocks shortcuts", () =>
            AssertTrue(SystemShortcutPolicy.ShouldBlock(
                isSessionActive: false,
                isAdminPanelOpen: false,
                isNetworkAuthOpen: false,
                isAlertOpen: false)));
        yield return ("internet auth blocks shortcuts", () =>
            AssertTrue(SystemShortcutPolicy.ShouldBlock(
                true, false, true, false)));
        yield return ("active alert blocks shortcuts", () =>
            AssertTrue(SystemShortcutPolicy.ShouldBlock(
                true, false, false, true)));
        yield return ("normal active session permits shortcuts", () =>
            AssertFalse(SystemShortcutPolicy.ShouldBlock(
                true, false, false, false)));
        yield return ("admin permits shortcuts", () =>
            AssertFalse(SystemShortcutPolicy.ShouldBlock(
                false, true, false, false)));
        yield return ("protected combinations are recognized",
            ProtectedCombinationsAreRecognized);
        yield return ("ordinary keys are permitted",
            OrdinaryKeysArePermitted);
        yield return ("warnings occur at approved thresholds",
            WarningsOccurAtApprovedThresholds);
        yield return ("short sessions skip higher warnings",
            ShortSessionsSkipHigherWarnings);
        yield return ("ten second warning is absent",
            TenSecondWarningIsAbsent);
    }

    private static void ProtectedCombinationsAreRecognized()
    {
        AssertTrue(SystemShortcutPolicy.IsBlockedShortcut(0x09, true, false));
        AssertTrue(SystemShortcutPolicy.IsBlockedShortcut(0x1B, true, false));
        AssertTrue(SystemShortcutPolicy.IsBlockedShortcut(0x73, true, false));
        AssertTrue(SystemShortcutPolicy.IsBlockedShortcut(0x1B, false, true));
        AssertTrue(SystemShortcutPolicy.IsBlockedShortcut(0x5B, false, false));
        AssertTrue(SystemShortcutPolicy.IsBlockedShortcut(0x5C, false, false));
    }

    private static void OrdinaryKeysArePermitted()
    {
        AssertFalse(SystemShortcutPolicy.IsBlockedShortcut(0x09, false, false));
        AssertFalse(SystemShortcutPolicy.IsBlockedShortcut(0x41, false, false));
    }

    private static void WarningsOccurAtApprovedThresholds()
    {
        AssertWarning(1801, 1800, 1800, "เหลือเวลาใช้งานอีก 30 นาที");
        AssertWarning(601, 600, 600, "เหลือเวลาใช้งานอีก 10 นาที");
        AssertWarning(61, 60, 60, "เหลือเวลาใช้งานอีก 1 นาที");
    }

    private static void ShortSessionsSkipHigherWarnings()
    {
        AssertNull(SessionWarningSchedule.GetCrossedWarning(1200, 1199));
        AssertNull(SessionWarningSchedule.GetCrossedWarning(300, 299));
    }

    private static void TenSecondWarningIsAbsent()
    {
        AssertNull(SessionWarningSchedule.GetCrossedWarning(11, 10));
    }

    private static void AssertWarning(
        int previous,
        int current,
        int expectedSeconds,
        string expectedMessage)
    {
        SessionWarning warning =
            SessionWarningSchedule.GetCrossedWarning(previous, current)
            ?? throw new InvalidOperationException("Expected warning.");

        AssertTrue(warning.RemainingSeconds == expectedSeconds);
        AssertTrue(warning.Message == expectedMessage);
    }

    private static void AssertTrue(bool condition)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Expected true.");
        }
    }

    private static void AssertFalse(bool condition)
    {
        AssertTrue(!condition);
    }

    private static void AssertNull(object? value)
    {
        if (value != null)
        {
            throw new InvalidOperationException("Expected null.");
        }
    }
}
