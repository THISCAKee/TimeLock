using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using TimeLockApp.Services;

internal static class TimelockApiClientTests
{
    public static IEnumerable<(string Name, Action Run)> All()
    {
        yield return ("machine events use scoped endpoint and device headers", GetEventsUsesScopedEndpointAndHeaders);
        yield return ("machine event acknowledgement preserves event id and null message", AcknowledgementPreservesEventId);
        yield return ("extension check sends only session id", ExtensionCheckSendsOnlySessionId);
        yield return ("extension confirmation sends idempotency key and reads server values", ConfirmationUsesIdempotencyKeyAndServerValues);
        yield return ("machine API exposes server error code and status", ServerErrorPreservesCodeAndStatus);
        yield return ("machine API rejects invalid JSON", InvalidJsonIsRejected);
        yield return ("machine API rejects a missing response envelope", MissingEnvelopeIsRejected);
        yield return ("machine API rejects an event missing its start time", MissingEventStartIsRejected);
        yield return ("machine API rejects an unsuccessful extension envelope", UnsuccessfulExtensionEnvelopeIsRejected);
        yield return ("machine API rejects extension eligibility without a reason", EligibilityWithoutReasonIsRejected);
        yield return ("machine API rejects confirmation without a booking id", ConfirmationWithoutBookingIsRejected);
    }

    private static void GetEventsUsesScopedEndpointAndHeaders()
    {
        RecordingHandler handler = RecordingHandler.Json(HttpStatusCode.OK, """
            {"events":[{"eventId":"event-id","eventType":"booking_confirmed","bookingId":"booking-id","bookingNumber":"BK-1","machineCode":"PC 001","startAt":"2026-08-20T10:00:00+07:00","endAt":"2026-08-20T12:00:00+07:00","username":"booking_0001","password":"secret"}]}
            """);
        using TimelockApiClient client = CreateClient(handler, "PC 001");

        IReadOnlyList<TimelockMachineEvent> events =
            client.GetEventsAsync(20).GetAwaiter().GetResult();

        AssertTrue(handler.Method == HttpMethod.Get, "Events must use GET.");
        AssertTrue(handler.PathAndQuery == "/api/machines/PC%20001/events?limit=20", "Machine code must be escaped in the event path.");
        AssertTrue(handler.Header("x-machine-code") == "PC 001", "Machine code header is required.");
        AssertTrue(handler.Header("x-device-token") == "token", "Device token header is required.");
        AssertTrue(!handler.PathAndQuery.Contains("secret", StringComparison.Ordinal), "Credentials must not enter the URL.");
        AssertTrue(events.Single().EventId == "event-id", "Event envelope must deserialize.");
    }

    private static void AcknowledgementPreservesEventId()
    {
        RecordingHandler handler = RecordingHandler.Json(HttpStatusCode.OK, "{}");
        using TimelockApiClient client = CreateClient(handler);
        DateTimeOffset processedAt = DateTimeOffset.Parse("2026-08-20T09:56:00+07:00");

        client.AcknowledgeEventAsync(
            "event/id",
            new TimelockEventAcknowledgement("processed", processedAt, null)).GetAwaiter().GetResult();

        AssertTrue(handler.PathAndQuery == "/api/machine-events/event%2Fid/ack", "Original event id must select the ACK endpoint.");
        using JsonDocument body = JsonDocument.Parse(handler.Body!);
        JsonElement root = body.RootElement;
        AssertTrue(root.GetProperty("status").GetString() == "processed", "ACK status must be processed.");
        AssertTrue(root.GetProperty("processedAt").GetDateTimeOffset() == processedAt, "ACK timestamp must retain its offset instant.");
        AssertTrue(root.TryGetProperty("message", out JsonElement message) && message.ValueKind == JsonValueKind.Null, "ACK must include a null message.");
    }

    private static void ExtensionCheckSendsOnlySessionId()
    {
        RecordingHandler handler = RecordingHandler.Json(HttpStatusCode.OK, """
            {"ok":true,"data":{"canExtend":true,"reason":"EXTENSION_AVAILABLE","currentEndAt":"2026-08-24T04:30:00Z","proposedEndAt":"2026-08-24T07:30:00Z","extensionCount":0,"maxExtensionCount":2}}
            """);
        using TimelockApiClient client = CreateClient(handler);

        TimelockExtensionCheck result =
            client.CheckExtensionAsync("session-id").GetAwaiter().GetResult();

        AssertTrue(handler.PathAndQuery == "/api/timelock/extension/check", "Check endpoint must match the contract.");
        AssertProperties(handler.Body!, "sessionId");
        AssertTrue(result.CanExtend && result.ProposedEndAt is not null, "Eligibility response must deserialize.");
    }

    private static void ConfirmationUsesIdempotencyKeyAndServerValues()
    {
        RecordingHandler handler = RecordingHandler.Json(HttpStatusCode.OK, """
            {"ok":true,"data":{"bookingId":"booking-id","endAt":"2026-08-24T07:30:00Z","extensionCount":1,"allowedMinutes":360}}
            """);
        using TimelockApiClient client = CreateClient(handler);
        Guid key = Guid.Parse("11111111-1111-1111-1111-111111111111");

        TimelockExtensionConfirmation result =
            client.ConfirmExtensionAsync("session-id", key).GetAwaiter().GetResult();

        AssertTrue(handler.PathAndQuery == "/api/timelock/extension/confirm", "Confirm endpoint must match the contract.");
        AssertProperties(handler.Body!, "sessionId", "idempotencyKey");
        using JsonDocument body = JsonDocument.Parse(handler.Body!);
        AssertTrue(body.RootElement.GetProperty("idempotencyKey").GetString() == key.ToString(), "Confirmation must send the supplied UUID.");
        AssertTrue(result.AllowedMinutes == 360 && result.ExtensionCount == 1, "Server allowance must deserialize unchanged.");
    }

    private static void ServerErrorPreservesCodeAndStatus()
    {
        RecordingHandler handler = RecordingHandler.Json(
            HttpStatusCode.Unauthorized,
            "{\"code\":\"MACHINE_TOKEN_INVALID\"}");
        using TimelockApiClient client = CreateClient(handler);

        TimelockApiException error = AssertThrows<TimelockApiException>(() =>
            client.GetEventsAsync().GetAwaiter().GetResult());

        AssertTrue(error.Code == "MACHINE_TOKEN_INVALID", "Server error code must be preserved.");
        AssertTrue(error.StatusCode == HttpStatusCode.Unauthorized, "HTTP status must be preserved.");
    }

    private static void InvalidJsonIsRejected()
    {
        RecordingHandler handler = RecordingHandler.Json(HttpStatusCode.OK, "not-json");
        using TimelockApiClient client = CreateClient(handler);

        TimelockApiException error = AssertThrows<TimelockApiException>(() =>
            client.GetEventsAsync().GetAwaiter().GetResult());

        AssertTrue(error.Code == "INVALID_SERVER_RESPONSE", "Invalid JSON must have a stable safe code.");
    }

    private static void MissingEnvelopeIsRejected()
    {
        RecordingHandler handler = RecordingHandler.Json(HttpStatusCode.OK, "{\"ok\":true}");
        using TimelockApiClient client = CreateClient(handler);

        TimelockApiException error = AssertThrows<TimelockApiException>(() =>
            client.GetEventsAsync().GetAwaiter().GetResult());

        AssertTrue(error.Code == "INVALID_SERVER_RESPONSE", "A missing events collection must be rejected.");
    }

    private static void MissingEventStartIsRejected()
    {
        RecordingHandler handler = RecordingHandler.Json(HttpStatusCode.OK, """
            {"events":[{"eventId":"event-id","eventType":"booking_confirmed","bookingId":"booking-id","bookingNumber":"BK-1","machineCode":"PC-001","endAt":"2026-08-20T12:00:00+07:00","username":"booking_0001","password":"secret"}]}
            """);
        using TimelockApiClient client = CreateClient(handler);

        TimelockApiException error = AssertThrows<TimelockApiException>(() =>
            client.GetEventsAsync().GetAwaiter().GetResult());

        AssertTrue(error.Code == "INVALID_SERVER_RESPONSE", "Missing event timestamps must be rejected.");
    }

    private static void UnsuccessfulExtensionEnvelopeIsRejected()
    {
        RecordingHandler handler = RecordingHandler.Json(HttpStatusCode.OK, """
            {"ok":false,"data":{"canExtend":true,"reason":"EXTENSION_AVAILABLE","currentEndAt":"2026-08-24T04:30:00Z","proposedEndAt":"2026-08-24T07:30:00Z","extensionCount":0,"maxExtensionCount":2}}
            """);
        using TimelockApiClient client = CreateClient(handler);

        TimelockApiException error = AssertThrows<TimelockApiException>(() =>
            client.CheckExtensionAsync("session-id").GetAwaiter().GetResult());

        AssertTrue(error.Code == "INVALID_SERVER_RESPONSE", "ok=false must not be treated as success.");
    }

    private static void EligibilityWithoutReasonIsRejected()
    {
        RecordingHandler handler = RecordingHandler.Json(HttpStatusCode.OK, """
            {"ok":true,"data":{"canExtend":false,"currentEndAt":"2026-08-24T04:30:00Z","proposedEndAt":null,"extensionCount":0,"maxExtensionCount":2}}
            """);
        using TimelockApiClient client = CreateClient(handler);

        TimelockApiException error = AssertThrows<TimelockApiException>(() =>
            client.CheckExtensionAsync("session-id").GetAwaiter().GetResult());

        AssertTrue(error.Code == "INVALID_SERVER_RESPONSE", "Eligibility reason is required.");
    }

    private static void ConfirmationWithoutBookingIsRejected()
    {
        RecordingHandler handler = RecordingHandler.Json(HttpStatusCode.OK, """
            {"ok":true,"data":{"endAt":"2026-08-24T07:30:00Z","extensionCount":1,"allowedMinutes":360}}
            """);
        using TimelockApiClient client = CreateClient(handler);

        TimelockApiException error = AssertThrows<TimelockApiException>(() =>
            client.ConfirmExtensionAsync("session-id", Guid.NewGuid()).GetAwaiter().GetResult());

        AssertTrue(error.Code == "INVALID_SERVER_RESPONSE", "Confirmation booking id is required.");
    }

    private static TimelockApiClient CreateClient(RecordingHandler handler, string machineCode = "PC-001") =>
        new(TimelockDeviceConfiguration.Create(
            machineCode,
            "token",
            "https://example.com",
            PasswordVerifier.Create("admin", iterations: 1_000)), handler);

    private static void AssertProperties(string json, params string[] expected)
    {
        using JsonDocument body = JsonDocument.Parse(json);
        string[] actual = body.RootElement.EnumerateObject().Select(property => property.Name).Order().ToArray();
        AssertTrue(actual.SequenceEqual(expected.Order()), $"Expected only JSON properties: {string.Join(", ", expected)}.");
    }

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

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;
        private readonly Dictionary<string, string> _headers = new(StringComparer.OrdinalIgnoreCase);

        private RecordingHandler(HttpStatusCode statusCode, string responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        public HttpMethod? Method { get; private set; }
        public string PathAndQuery { get; private set; } = "";
        public string? Body { get; private set; }

        public static RecordingHandler Json(HttpStatusCode statusCode, string responseBody) =>
            new(statusCode, responseBody);

        public string? Header(string name) => _headers.GetValueOrDefault(name);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method;
            PathAndQuery = request.RequestUri!.PathAndQuery;
            foreach ((string name, IEnumerable<string> values) in request.Headers)
            {
                _headers[name] = string.Join(",", values);
            }
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
