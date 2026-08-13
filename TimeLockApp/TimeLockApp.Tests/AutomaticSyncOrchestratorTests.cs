using TimeLockApp.Services;

internal static class AutomaticSyncOrchestratorTests
{
    public static IEnumerable<(string Name, Action Run)> All()
    {
        yield return (
            "automatic sync interval is ten seconds",
            IntervalIsTenSeconds);
        yield return (
            "all automatic sync triggers use the same path",
            () => AllTriggersUseSamePathAsync().GetAwaiter().GetResult());
        yield return (
            "automatic sync triggers never overlap",
            () => TriggersNeverOverlapAsync().GetAwaiter().GetResult());
    }

    private static void IntervalIsTenSeconds()
    {
        AssertTrue(
            AutomaticSyncOrchestrator.Interval == TimeSpan.FromSeconds(10),
            "Automatic synchronization must run every 10 seconds.");
    }

    private static async Task AllTriggersUseSamePathAsync()
    {
        int calls = 0;
        var completions = new List<AutomaticSyncCompletedEventArgs>();
        var orchestrator = new AutomaticSyncOrchestrator(_ =>
        {
            calls++;
            return Task.FromResult(UserSyncResult.Success(7, hasChanges: true));
        });
        orchestrator.Completed += (_, args) => completions.Add(args);

        AutomaticSyncTrigger[] triggers =
        {
            AutomaticSyncTrigger.Startup,
            AutomaticSyncTrigger.InternetAuthenticated,
            AutomaticSyncTrigger.Periodic,
            AutomaticSyncTrigger.SessionExpired,
            AutomaticSyncTrigger.Logout
        };

        foreach (AutomaticSyncTrigger trigger in triggers)
        {
            UserSyncResult result = await orchestrator.RunAsync(trigger);
            AssertTrue(result.IsSuccessful, $"{trigger} must return the sync result.");
        }

        AssertTrue(calls == triggers.Length, "Every trigger must invoke synchronization.");
        AssertTrue(completions.Count == triggers.Length, "Every trigger must publish completion.");

        for (int index = 0; index < triggers.Length; index++)
        {
            AssertTrue(
                completions[index].Trigger == triggers[index] &&
                completions[index].Result.HasChanges,
                $"Completion {index} must preserve its trigger and result.");
        }
    }

    private static async Task TriggersNeverOverlapAsync()
    {
        int calls = 0;
        int active = 0;
        int maximumActive = 0;
        var firstStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var orchestrator = new AutomaticSyncOrchestrator(async cancellationToken =>
        {
            int call = Interlocked.Increment(ref calls);
            int currentActive = Interlocked.Increment(ref active);
            UpdateMaximum(ref maximumActive, currentActive);

            try
            {
                if (call == 1)
                {
                    firstStarted.TrySetResult();
                    await releaseFirst.Task.WaitAsync(cancellationToken);
                }

                return UserSyncResult.Success(1, hasChanges: false);
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
        });

        Task<UserSyncResult> first = orchestrator.RunAsync(
            AutomaticSyncTrigger.Periodic);
        await firstStarted.Task;

        Task<UserSyncResult> second = orchestrator.RunAsync(
            AutomaticSyncTrigger.Logout);
        await Task.Yield();

        AssertTrue(calls == 1, "The second trigger must wait for the first.");

        releaseFirst.TrySetResult();
        await Task.WhenAll(first, second);

        AssertTrue(maximumActive == 1, "Automatic sync operations must not overlap.");
    }

    private static void UpdateMaximum(ref int maximum, int value)
    {
        int current;
        do
        {
            current = Volatile.Read(ref maximum);
            if (current >= value)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref maximum, value, current) != current);
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
