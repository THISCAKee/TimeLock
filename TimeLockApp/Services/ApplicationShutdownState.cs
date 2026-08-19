namespace TimeLockApp.Services;

public sealed class ApplicationShutdownState
{
    public bool IsRequested { get; private set; }

    public void Request(Action shutdown)
    {
        if (IsRequested)
        {
            return;
        }

        IsRequested = true;
        shutdown();
    }
}
