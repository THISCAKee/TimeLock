namespace TimeLockApp.Services;

internal sealed record SessionWarning(
    int RemainingSeconds,
    string Message);

internal static class SessionWarningSchedule
{
    private static readonly SessionWarning[] Warnings =
    {
        new(1800, "เหลือเวลาใช้งานอีก 30 นาที"),
        new(600, "เหลือเวลาใช้งานอีก 10 นาที"),
        new(60, "เหลือเวลาใช้งานอีก 1 นาที")
    };

    internal static SessionWarning? GetCrossedWarning(
        int previousSeconds,
        int currentSeconds)
    {
        if (currentSeconds >= previousSeconds)
        {
            return null;
        }

        return Warnings.FirstOrDefault(warning =>
            previousSeconds > warning.RemainingSeconds &&
            currentSeconds <= warning.RemainingSeconds);
    }
}
