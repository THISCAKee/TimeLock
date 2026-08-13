using TimeLockApp.Services;

static class ShutdownServiceTests
{
    public static IEnumerable<(string Name, Action Run)> All()
    {
        yield return ("shutdown command force-closes blocking applications", UsesForceShutdownArguments);
    }

    private static void UsesForceShutdownArguments()
    {
        var startInfo = ShutdownService.CreateStartInfo();

        AssertTrue(
            Path.GetFileName(startInfo.FileName).Equals(
                "shutdown.exe",
                StringComparison.OrdinalIgnoreCase),
            "Shutdown must use the Windows shutdown executable.");
        AssertTrue(
            startInfo.Arguments == "/s /f /t 0",
            "Shutdown must force-close applications that block shutdown.");
        AssertTrue(
            !startInfo.UseShellExecute,
            "Shutdown must invoke shutdown.exe directly.");
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
