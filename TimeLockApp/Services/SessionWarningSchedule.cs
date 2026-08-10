namespace TimeLockApp.Services;

internal sealed record SessionWarning(
    int RemainingSeconds,
    string Message);

internal static class SessionWarningSchedule
{
    private static readonly (int RemainingSeconds, string MessageKey)[] Warnings =
    {
        (1800, "Warning30Minutes"),
        (600, "Warning10Minutes"),
        (60, "Warning1Minute")
    };

    internal static SessionWarning? GetCrossedWarning(
        int previousSeconds,
        int currentSeconds)
    {
        if (currentSeconds >= previousSeconds)
        {
            return null;
        }

        (int RemainingSeconds, string MessageKey) crossedWarning = Warnings.FirstOrDefault(warning =>
            previousSeconds > warning.RemainingSeconds &&
            currentSeconds <= warning.RemainingSeconds);

        return crossedWarning.MessageKey != null
            ? new SessionWarning(
                crossedWarning.RemainingSeconds,
                LanguageService.Default.Get(crossedWarning.MessageKey))
            : null;
    }
}
