using TimeLockApp.Services;

internal static class SessionTimeTests
{
    public static IEnumerable<(string Name, Action Run)> All()
    {
        yield return ("extension time uses server end and cumulative allowance", UsesServerEndAndAllowance);
        yield return ("extension time clamps an expired server end", ClampsExpiredEnd);
        yield return ("extension time rejects negative server allowance", RejectsNegativeAllowance);
    }

    private static void UsesServerEndAndAllowance()
    {
        DateTimeOffset now = DateTimeOffset.Parse("2026-08-24T04:30:00Z");
        TimelockExtensionConfirmation confirmation = new(
            "booking-id",
            DateTimeOffset.Parse("2026-08-24T07:30:00Z"),
            1,
            360);

        SessionTime result = SessionTime.FromExtension(confirmation, now);

        AssertTrue(result.RemainingSeconds == 10_800, "Remaining time must come from server endAt.");
        AssertTrue(result.TotalAllowedSeconds == 21_600, "Total allowance must come from server allowedMinutes.");
    }

    private static void ClampsExpiredEnd()
    {
        TimelockExtensionConfirmation confirmation = new(
            "booking-id",
            DateTimeOffset.Parse("2026-08-24T04:29:00Z"),
            1,
            360);

        SessionTime result = SessionTime.FromExtension(
            confirmation,
            DateTimeOffset.Parse("2026-08-24T04:30:00Z"));

        AssertTrue(result.RemainingSeconds == 0, "Past server endAt must not create negative time.");
    }

    private static void RejectsNegativeAllowance()
    {
        TimelockExtensionConfirmation confirmation = new(
            "booking-id",
            DateTimeOffset.Parse("2026-08-24T07:30:00Z"),
            1,
            -1);

        AssertThrows<ArgumentOutOfRangeException>(() =>
            SessionTime.FromExtension(confirmation, DateTimeOffset.UtcNow));
    }

    private static T AssertThrows<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T error)
        {
            return error;
        }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
