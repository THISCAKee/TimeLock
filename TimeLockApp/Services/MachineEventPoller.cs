namespace TimeLockApp.Services;

internal sealed class MachineEventPoller : IDisposable
{
    private const string SupportedEventType = "booking_confirmed";

    private readonly ITimelockMachineApi _api;
    private readonly Func<DateTimeOffset> _now;
    private readonly TimeSpan _interval;
    private readonly CancellationTokenSource _stop = new();
    private Task? _runTask;

    internal MachineEventPoller(
        ITimelockMachineApi api,
        Func<DateTimeOffset>? now = null,
        TimeSpan? interval = null)
    {
        _api = api;
        _now = now ?? (() => DateTimeOffset.UtcNow);
        _interval = interval ?? TimeSpan.FromSeconds(15);
    }

    internal void Start() =>
        _runTask ??= Task.Run(() => RunAsync(_stop.Token));

    internal async Task ProcessOnceAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<TimelockMachineEvent> events =
            await _api.GetEventsAsync(20, cancellationToken).ConfigureAwait(false);

        foreach (TimelockMachineEvent machineEvent in events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsSupportedAndValid(machineEvent))
            {
                continue;
            }

            TimelockEventAcknowledgement acknowledgement = new(
                "processed",
                _now(),
                null);

            try
            {
                await _api.AcknowledgeEventAsync(
                    machineEvent.EventId,
                    acknowledgement,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (TimelockApiException error)
                when (error.Code == "EVENT_ALREADY_PROCESSED")
            {
                // The server has already completed this idempotent event.
            }
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using PeriodicTimer timer = new(_interval);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOnceAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // The next cycle retries without logging event payloads or credentials.
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private static bool IsSupportedAndValid(TimelockMachineEvent machineEvent) =>
        string.Equals(
            machineEvent.EventType,
            SupportedEventType,
            StringComparison.Ordinal) &&
        !string.IsNullOrWhiteSpace(machineEvent.EventId) &&
        !string.IsNullOrWhiteSpace(machineEvent.BookingId) &&
        !string.IsNullOrWhiteSpace(machineEvent.BookingNumber) &&
        !string.IsNullOrWhiteSpace(machineEvent.MachineCode) &&
        !string.IsNullOrWhiteSpace(machineEvent.Username) &&
        !string.IsNullOrWhiteSpace(machineEvent.Password) &&
        machineEvent.EndAt > machineEvent.StartAt;

    public void Dispose()
    {
        _stop.Cancel();
        try
        {
            _runTask?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }
        _stop.Dispose();
    }
}
