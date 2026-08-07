namespace TimeLockApp.Services;

internal static class SystemShortcutPolicy
{
    internal static bool ShouldBlock(
        bool isSessionActive,
        bool isAdminPanelOpen,
        bool isNetworkAuthOpen,
        bool isAlertOpen)
    {
        return isNetworkAuthOpen ||
               isAlertOpen ||
               (!isSessionActive && !isAdminPanelOpen);
    }

    internal static bool IsBlockedShortcut(
        int virtualKey,
        bool altPressed,
        bool controlPressed)
    {
        return virtualKey == 0x5B ||
               virtualKey == 0x5C ||
               (altPressed &&
                (virtualKey == 0x09 ||
                 virtualKey == 0x1B ||
                 virtualKey == 0x73)) ||
               (controlPressed && virtualKey == 0x1B);
    }
}
