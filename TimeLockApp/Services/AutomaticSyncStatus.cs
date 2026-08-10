namespace TimeLockApp.Services;

internal static class AutomaticSyncStatus
{
    internal static string Format(
        UserSyncResult result,
        DateTime completedAt)
    {
        if (result.IsSuccessful)
        {
            return LanguageServiceForStatus.Get(
                "LatestSync",
                completedAt,
                result.UserCount);
        }

        return LanguageServiceForStatus.Get(
            "LatestSyncFailed",
            result.ErrorMessage);
    }

    private static LanguageService LanguageServiceForStatus =>
        LanguageService.Default;
}
