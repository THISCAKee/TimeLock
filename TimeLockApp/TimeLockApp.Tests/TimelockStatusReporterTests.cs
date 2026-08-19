using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using TimeLockApp.Services;

static class TimelockStatusReporterTests
{
    public static IEnumerable<(string Name, Action Run)> All()
    {
        yield return (
            "status reporter can be disposed while UI synchronization context is blocked",
            DisposeDoesNotWaitOnUiContext);
    }

    private static void DisposeDoesNotWaitOnUiContext()
    {
        TimelockDeviceConfiguration configuration =
            TimelockDeviceConfiguration.Create(
                "pc-001",
                "token",
                "https://example.com",
                PasswordVerifier.Create("password", iterations: 1_000));

        using TimelockApiClient client = new(
            configuration,
            new BlockingHandler());
        TimelockStatusReporter reporter = new(
            client,
            configuration,
            () => null);
        BlockingSynchronizationContext context = new();
        SynchronizationContext? previousContext =
            SynchronizationContext.Current;

        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            reporter.Start();
            SynchronizationContext.SetSynchronizationContext(previousContext);

            Task disposeTask = Task.Run(reporter.Dispose);
            AssertTrue(
                disposeTask.Wait(TimeSpan.FromSeconds(1)),
                "Disposing the reporter must not wait on the UI dispatcher.");
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previousContext);
            context.Pump();
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class BlockingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class BlockingSynchronizationContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _queue = new();

        public override void Post(SendOrPostCallback d, object? state) =>
            _queue.Enqueue((d, state));

        public void Pump()
        {
            while (_queue.TryDequeue(out (SendOrPostCallback Callback, object? State) work))
            {
                work.Callback(work.State);
            }
        }
    }
}
