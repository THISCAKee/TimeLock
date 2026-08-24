using System.Net.Http;
using TimeLockApp.Services;

internal static class SessionExtensionControllerTests
{
    public static IEnumerable<(string Name, Action Run)> All()
    {
        yield return ("extension check returns server eligibility", CheckReturnsServerEligibility);
        yield return ("extension check transport failure fails closed", CheckTransportFailureFailsClosed);
        yield return ("extension controller rejects overlapping expiry flows", OverlappingExpiryIsRejected);
        yield return ("transient extension retry reuses idempotency key", TransientRetryReusesKey);
        yield return ("explicit extension rejection completes attempt", ExplicitRejectionCompletesAttempt);
        yield return ("later extension action rotates idempotency key", LaterActionRotatesKey);
    }

    private static void CheckReturnsServerEligibility()
    {
        FakeMachineApi api = new() { CheckResult = Eligible() };
        SessionExtensionController controller = new(api);

        ExtensionEligibilityResult result =
            controller.BeginExpiryAsync("session-id").GetAwaiter().GetResult();

        AssertTrue(result.CanExtend, "Server eligibility must be exposed.");
        AssertTrue(result.Reason == "EXTENSION_AVAILABLE", "Server reason must be preserved.");
    }

    private static void CheckTransportFailureFailsClosed()
    {
        FakeMachineApi api = new() { CheckError = new HttpRequestException("offline") };
        SessionExtensionController controller = new(api);

        ExtensionEligibilityResult result =
            controller.BeginExpiryAsync("session-id").GetAwaiter().GetResult();

        AssertTrue(!result.CanExtend, "Unavailable checks must not expose extension.");
        AssertTrue(result.Reason == "EXTENSION_CHECK_UNAVAILABLE", "Unavailable checks need a stable reason.");
    }

    private static void OverlappingExpiryIsRejected()
    {
        FakeMachineApi api = new() { CheckResult = Eligible() };
        SessionExtensionController controller = new(api);
        controller.BeginExpiryAsync("session-id").GetAwaiter().GetResult();

        AssertThrows<InvalidOperationException>(() =>
            controller.BeginExpiryAsync("other-session").GetAwaiter().GetResult());
    }

    private static void TransientRetryReusesKey()
    {
        Guid key = Guid.Parse("11111111-1111-1111-1111-111111111111");
        FakeMachineApi api = new() { CheckResult = Eligible() };
        api.ConfirmResults.Enqueue(new HttpRequestException("offline"));
        api.ConfirmResults.Enqueue(Confirmation());
        SessionExtensionController controller = new(api, () => key);
        controller.BeginExpiryAsync("session-id").GetAwaiter().GetResult();

        ExtensionConfirmationResult first =
            controller.ConfirmAsync().GetAwaiter().GetResult();
        ExtensionConfirmationResult second =
            controller.ConfirmAsync().GetAwaiter().GetResult();

        AssertTrue(first.Outcome == ExtensionConfirmOutcome.RetryableFailure, "Transport failure must permit retry.");
        AssertTrue(second.Outcome == ExtensionConfirmOutcome.Extended, "Retry may complete extension.");
        AssertTrue(api.ConfirmKeys.SequenceEqual(new[] { key, key }), "The same action must reuse its UUID.");
    }

    private static void ExplicitRejectionCompletesAttempt()
    {
        FakeMachineApi api = new() { CheckResult = Eligible() };
        api.ConfirmResults.Enqueue(new TimelockApiException("EXTENSION_NEXT_BOOKING_CONFLICT"));
        SessionExtensionController controller = new(api);
        controller.BeginExpiryAsync("session-id").GetAwaiter().GetResult();

        ExtensionConfirmationResult result =
            controller.ConfirmAsync().GetAwaiter().GetResult();

        AssertTrue(result.Outcome == ExtensionConfirmOutcome.Rejected, "Server denial must be terminal.");
        AssertTrue(result.ErrorCode == "EXTENSION_NEXT_BOOKING_CONFLICT", "Denial code must reach the UI.");
        AssertThrows<InvalidOperationException>(() =>
            controller.ConfirmAsync().GetAwaiter().GetResult());
    }

    private static void LaterActionRotatesKey()
    {
        Guid firstKey = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid secondKey = Guid.Parse("22222222-2222-2222-2222-222222222222");
        Queue<Guid> keys = new([firstKey, secondKey]);
        FakeMachineApi api = new() { CheckResult = Eligible() };
        api.ConfirmResults.Enqueue(Confirmation());
        api.ConfirmResults.Enqueue(Confirmation());
        SessionExtensionController controller = new(api, keys.Dequeue);

        controller.BeginExpiryAsync("session-1").GetAwaiter().GetResult();
        controller.ConfirmAsync().GetAwaiter().GetResult();
        controller.BeginExpiryAsync("session-2").GetAwaiter().GetResult();
        controller.ConfirmAsync().GetAwaiter().GetResult();

        AssertTrue(api.ConfirmKeys.SequenceEqual(new[] { firstKey, secondKey }), "A later user action needs a new UUID.");
    }

    private static TimelockExtensionCheck Eligible() =>
        new(
            true,
            "EXTENSION_AVAILABLE",
            DateTimeOffset.Parse("2026-08-24T04:30:00Z"),
            DateTimeOffset.Parse("2026-08-24T07:30:00Z"),
            0,
            2);

    private static TimelockExtensionConfirmation Confirmation() =>
        new(
            "booking-id",
            DateTimeOffset.Parse("2026-08-24T07:30:00Z"),
            1,
            360);

    private static T AssertThrows<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T error)
        {
            return error;
        }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class FakeMachineApi : ITimelockMachineApi
    {
        public TimelockExtensionCheck CheckResult { get; init; } = Eligible();
        public Exception? CheckError { get; init; }
        public Queue<object> ConfirmResults { get; } = new();
        public List<Guid> ConfirmKeys { get; } = [];

        public Task<TimelockExtensionCheck> CheckExtensionAsync(
            string sessionId,
            CancellationToken cancellationToken = default) =>
            CheckError is null
                ? Task.FromResult(CheckResult)
                : Task.FromException<TimelockExtensionCheck>(CheckError);

        public Task<TimelockExtensionConfirmation> ConfirmExtensionAsync(
            string sessionId,
            Guid idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            ConfirmKeys.Add(idempotencyKey);
            object result = ConfirmResults.Dequeue();
            return result is Exception error
                ? Task.FromException<TimelockExtensionConfirmation>(error)
                : Task.FromResult((TimelockExtensionConfirmation)result);
        }

        public Task<IReadOnlyList<TimelockMachineEvent>> GetEventsAsync(
            int limit = 20,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AcknowledgeEventAsync(
            string eventId,
            TimelockEventAcknowledgement acknowledgement,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
