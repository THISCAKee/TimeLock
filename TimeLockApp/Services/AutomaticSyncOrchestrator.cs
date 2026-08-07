namespace TimeLockApp.Services;

internal enum AutomaticSyncTrigger
{
    Startup,
    InternetAuthenticated,
    Periodic,
    SessionExpired,
    Logout
}

internal sealed record AutomaticSyncCompletedEventArgs(
    AutomaticSyncTrigger Trigger,
    UserSyncResult Result);

internal sealed class AutomaticSyncOrchestrator
{
    internal static readonly TimeSpan Interval =
        TimeSpan.FromSeconds(30);

    private readonly Func<CancellationToken, Task<UserSyncResult>> _synchronize;
    private readonly SemaphoreSlim _gate = new(1, 1);

    internal AutomaticSyncOrchestrator(
        Func<CancellationToken, Task<UserSyncResult>> synchronize)
    {
        ArgumentNullException.ThrowIfNull(synchronize);
        _synchronize = synchronize;
    }

    internal event EventHandler<AutomaticSyncCompletedEventArgs>? Completed;

    internal async Task<UserSyncResult> RunAsync(
        AutomaticSyncTrigger trigger,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);

        try
        {
            UserSyncResult result =
                await _synchronize(cancellationToken);

            Completed?.Invoke(
                this,
                new AutomaticSyncCompletedEventArgs(trigger, result));

            return result;
        }
        finally
        {
            _gate.Release();
        }
    }
}
