using System.Globalization;

namespace TimeLockApp.Services;

internal static class AutomaticSyncStatus
{
    internal static string Format(
        UserSyncResult result,
        DateTime completedAt)
    {
        if (result.IsSuccessful)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "ซิงค์ล่าสุด {0:HH:mm:ss}: {1} รายการ",
                completedAt,
                result.UserCount);
        }

        return $"ซิงค์ไม่สำเร็จ: {result.ErrorMessage} " +
               "(จะลองใหม่อัตโนมัติ)";
    }
}
