using TimeLockApp.Services;

static class ApplicationShutdownStateTests
{
    public static IEnumerable<(string Name, Action Run)> All()
    {
        yield return (
            "shutdown request is visible before shutdown callback runs",
            RequestMarksStateBeforeCallback);
    }

    private static void RequestMarksStateBeforeCallback()
    {
        ApplicationShutdownState state = new();
        bool callbackSawRequestedState = false;

        state.Request(() => callbackSawRequestedState = state.IsRequested);

        AssertTrue(
            callbackSawRequestedState,
            "The closing guard must be released before Application.Shutdown runs.");
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
