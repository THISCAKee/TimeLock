using System.Net.Http;
using TimeLockApp.Services;

internal static class MachineEventPollerTests
{
    private static readonly DateTimeOffset FixedNow =
        DateTimeOffset.Parse("2026-08-20T09:56:00+07:00");

    public static IEnumerable<(string Name, Action Run)> All()
    {
        yield return ("booking confirmed acknowledgement uses original event id", BookingConfirmedUsesOriginalEventId);
        yield return ("unknown machine event remains unacknowledged", UnknownEventIsNotAcknowledged);
        yield return ("malformed booking event remains unacknowledged", MalformedBookingEventIsNotAcknowledged);
        yield return ("already processed event acknowledgement is complete", AlreadyProcessedAcknowledgementIsComplete);
        yield return ("machine event poller disposal cancels blocked request", DisposalCancelsBlockedRequest);
    }

    private static void BookingConfirmedUsesOriginalEventId()
    {
        FakeMachineApi api = new(Event("event-123", "booking_confirmed"));
        using MachineEventPoller poller = new(api, () => FixedNow);

        poller.ProcessOnceAsync(CancellationToken.None).GetAwaiter().GetResult();

        AcknowledgedEvent acknowledged = api.Acknowledgements.Single();
        AssertTrue(acknowledged.EventId == "event-123", "ACK must use the original event id.");
        AssertTrue(
            acknowledged.Body == new TimelockEventAcknowledgement("processed", FixedNow, null),
            "ACK body must identify successful processing.");
    }

    private static void UnknownEventIsNotAcknowledged()
    {
        FakeMachineApi api = new(Event("event-unknown", "booking_cancelled"));
        using MachineEventPoller poller = new(api, () => FixedNow);

        poller.ProcessOnceAsync(CancellationToken.None).GetAwaiter().GetResult();

        AssertTrue(api.Acknowledgements.Count == 0, "Unknown event types must remain for a future client.");
    }

    private static void MalformedBookingEventIsNotAcknowledged()
    {
        TimelockMachineEvent malformed = Event("event-bad", "booking_confirmed") with
        {
            BookingId = ""
        };
        FakeMachineApi api = new(malformed);
        using MachineEventPoller poller = new(api, () => FixedNow);

        poller.ProcessOnceAsync(CancellationToken.None).GetAwaiter().GetResult();

        AssertTrue(api.Acknowledgements.Count == 0, "Malformed supported events must not be ACKed.");
    }

    private static void AlreadyProcessedAcknowledgementIsComplete()
    {
        FakeMachineApi api = new(Event("event-duplicate", "booking_confirmed"))
        {
            AckError = new TimelockApiException("EVENT_ALREADY_PROCESSED")
        };
        using MachineEventPoller poller = new(api, () => FixedNow);

        poller.ProcessOnceAsync(CancellationToken.None).GetAwaiter().GetResult();

        AssertTrue(api.Acknowledgements.Count == 1, "Duplicate ACK must be treated as completed work.");
    }

    private static void DisposalCancelsBlockedRequest()
    {
        FakeMachineApi api = new() { BlockGetEvents = true };
        MachineEventPoller poller = new(api, () => FixedNow);
        poller.Start();
        AssertTrue(api.GetStarted.Wait(TimeSpan.FromSeconds(1)), "Polling must start immediately.");

        Task dispose = Task.Run(poller.Dispose);

        AssertTrue(dispose.Wait(TimeSpan.FromSeconds(1)), "Dispose must cancel an in-flight poll promptly.");
    }

    private static TimelockMachineEvent Event(string id, string type) =>
        new(
            id,
            type,
            "booking-id",
            "BK-1",
            "PC-001",
            DateTimeOffset.Parse("2026-08-20T10:00:00+07:00"),
            DateTimeOffset.Parse("2026-08-20T12:00:00+07:00"),
            "booking_0001",
            "secret");

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed record AcknowledgedEvent(
        string EventId,
        TimelockEventAcknowledgement Body);

    private sealed class FakeMachineApi(params TimelockMachineEvent[] events) : ITimelockMachineApi
    {
        public List<AcknowledgedEvent> Acknowledgements { get; } = [];
        public TimelockApiException? AckError { get; init; }
        public bool BlockGetEvents { get; init; }
        public ManualResetEventSlim GetStarted { get; } = new(false);

        public async Task<IReadOnlyList<TimelockMachineEvent>> GetEventsAsync(
            int limit = 20,
            CancellationToken cancellationToken = default)
        {
            GetStarted.Set();
            if (BlockGetEvents)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            return events;
        }

        public Task AcknowledgeEventAsync(
            string eventId,
            TimelockEventAcknowledgement acknowledgement,
            CancellationToken cancellationToken = default)
        {
            Acknowledgements.Add(new AcknowledgedEvent(eventId, acknowledgement));
            return AckError is null
                ? Task.CompletedTask
                : Task.FromException(AckError);
        }

        public Task<TimelockExtensionCheck> CheckExtensionAsync(
            string sessionId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TimelockExtensionConfirmation> ConfirmExtensionAsync(
            string sessionId,
            Guid idempotencyKey,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
