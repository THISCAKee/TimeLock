using System.Net.Http;
using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TimeLockApp.Services;

internal sealed class TimelockApiException(
    string code,
    HttpStatusCode? statusCode = null) : Exception(code)
{
    internal string Code { get; } = code;
    internal HttpStatusCode? StatusCode { get; } = statusCode;
}

internal sealed class TimelockApiClient : ITimelockMachineApi, IDisposable
{
    private readonly HttpClient _http;
    private readonly TimelockDeviceConfiguration _configuration;

    internal TimelockApiClient(
        TimelockDeviceConfiguration configuration,
        HttpMessageHandler? handler = null)
    {
        _configuration = configuration;
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.BaseAddress = new Uri(configuration.BackendUrl + "/");
        _http.Timeout = TimeSpan.FromSeconds(15);
        _http.DefaultRequestHeaders.Add("x-machine-code", configuration.MachineCode);
        _http.DefaultRequestHeaders.Add("x-device-token", configuration.DeviceToken);
    }

    internal async Task<(IReadOnlyList<TimelockOfflineAccount> Accounts, DateTimeOffset ServerTime)> SyncAsync(
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync(
            "api/timelock/sync", new { }, cancellationToken);
        SyncResponse body = await ReadAsync<SyncResponse>(response, cancellationToken);
        DateTimeOffset serverTime = response.Headers.Date ?? DateTimeOffset.UtcNow;
        return (body.Accounts, serverTime);
    }

    internal async Task<TimelockLoginSession> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync(
            "api/timelock/login", new { username, password }, cancellationToken);
        LoginResponse body = await ReadAsync<LoginResponse>(response, cancellationToken);
        return body.Session;
    }

    internal async Task LogoutAsync(
        string sessionId,
        int usedSeconds,
        string status,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync(
            "api/timelock/logout",
            new { sessionId, usedSeconds, status },
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    internal async Task SendHeartbeatAsync(
        TimelockHeartbeat heartbeat,
        CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "api/machines/heartbeat")
        {
            Content = JsonContent.Create(heartbeat)
        };
        // Existing heartbeat endpoint reads MachineCode from the body and token from this header.
        using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    internal async Task ReconcileOfflineSessionAsync(
        TimelockLoginSession session,
        int usedSeconds,
        string status,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await _http.PostAsJsonAsync(
            "api/timelock/offline-session",
            new
            {
                clientSessionId = session.SessionId,
                username = session.Username,
                startedAt = session.StartedAt,
                endedAt = DateTimeOffset.UtcNow,
                usedSeconds,
                status
            },
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<TimelockMachineEvent>> GetEventsAsync(
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        string machineCode = Uri.EscapeDataString(_configuration.MachineCode);
        using HttpResponseMessage response = await _http.GetAsync(
            $"api/machines/{machineCode}/events?limit={limit}",
            cancellationToken);
        EventsResponse body = await ReadAsync<EventsResponse>(response, cancellationToken);
        IReadOnlyList<TimelockMachineEvent> events = body.Events ??
            throw InvalidResponse(response.StatusCode);
        if (events.Any(machineEvent => !IsValid(machineEvent)))
        {
            throw InvalidResponse(response.StatusCode);
        }
        return events;
    }

    public async Task AcknowledgeEventAsync(
        string eventId,
        TimelockEventAcknowledgement acknowledgement,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentNullException.ThrowIfNull(acknowledgement);

        string escapedEventId = Uri.EscapeDataString(eventId);
        using HttpResponseMessage response = await _http.PostAsJsonAsync(
            $"api/machine-events/{escapedEventId}/ack",
            acknowledgement,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<TimelockExtensionCheck> CheckExtensionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        using HttpResponseMessage response = await _http.PostAsJsonAsync(
            "api/timelock/extension/check",
            new { sessionId },
            cancellationToken);
        DataResponse<TimelockExtensionCheck> body =
            await ReadAsync<DataResponse<TimelockExtensionCheck>>(response, cancellationToken);
        TimelockExtensionCheck check = body.Data ??
            throw InvalidResponse(response.StatusCode);
        if (!body.Ok || !IsValid(check))
        {
            throw InvalidResponse(response.StatusCode);
        }
        return check;
    }

    public async Task<TimelockExtensionConfirmation> ConfirmExtensionAsync(
        string sessionId,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        if (idempotencyKey == Guid.Empty)
        {
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
        }

        using HttpResponseMessage response = await _http.PostAsJsonAsync(
            "api/timelock/extension/confirm",
            new { sessionId, idempotencyKey },
            cancellationToken);
        DataResponse<TimelockExtensionConfirmation> body =
            await ReadAsync<DataResponse<TimelockExtensionConfirmation>>(response, cancellationToken);
        TimelockExtensionConfirmation confirmation = body.Data ??
            throw InvalidResponse(response.StatusCode);
        if (!body.Ok || !IsValid(confirmation))
        {
            throw InvalidResponse(response.StatusCode);
        }
        return confirmation;
    }

    private static bool IsValid(TimelockMachineEvent machineEvent) =>
        machineEvent is not null &&
        !string.IsNullOrWhiteSpace(machineEvent.EventId) &&
        !string.IsNullOrWhiteSpace(machineEvent.EventType) &&
        !string.IsNullOrWhiteSpace(machineEvent.BookingId) &&
        !string.IsNullOrWhiteSpace(machineEvent.BookingNumber) &&
        !string.IsNullOrWhiteSpace(machineEvent.MachineCode) &&
        !string.IsNullOrWhiteSpace(machineEvent.Username) &&
        !string.IsNullOrWhiteSpace(machineEvent.Password) &&
        machineEvent.StartAt != default &&
        machineEvent.EndAt != default &&
        machineEvent.EndAt > machineEvent.StartAt;

    private static bool IsValid(TimelockExtensionCheck check) =>
        !string.IsNullOrWhiteSpace(check.Reason) &&
        check.CurrentEndAt != default &&
        check.ExtensionCount >= 0 &&
        check.MaxExtensionCount >= check.ExtensionCount &&
        (check.CanExtend
            ? check.ProposedEndAt is not null &&
              check.ProposedEndAt > check.CurrentEndAt
            : check.ProposedEndAt is null);

    private static bool IsValid(TimelockExtensionConfirmation confirmation) =>
        !string.IsNullOrWhiteSpace(confirmation.BookingId) &&
        confirmation.EndAt != default &&
        confirmation.ExtensionCount >= 0 &&
        confirmation.AllowedMinutes > 0;

    private static TimelockApiException InvalidResponse(HttpStatusCode statusCode) =>
        new("INVALID_SERVER_RESPONSE", statusCode);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            await ThrowApiError(response, cancellationToken);
        }
        try
        {
            T? value = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
            return value ?? throw new TimelockApiException("INVALID_SERVER_RESPONSE", response.StatusCode);
        }
        catch (JsonException)
        {
            throw new TimelockApiException("INVALID_SERVER_RESPONSE", response.StatusCode);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode) await ThrowApiError(response, cancellationToken);
    }

    private static async Task ThrowApiError(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            ApiError? error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: cancellationToken);
            throw new TimelockApiException(
                error?.Code ?? "SERVER_REQUEST_FAILED",
                response.StatusCode);
        }
        catch (JsonException)
        {
            throw new TimelockApiException("SERVER_REQUEST_FAILED", response.StatusCode);
        }
    }

    public void Dispose() => _http.Dispose();

    private sealed record ApiError([property: JsonPropertyName("code")] string Code);
    private sealed record SyncResponse([property: JsonPropertyName("accounts")] IReadOnlyList<TimelockOfflineAccount> Accounts);
    private sealed record LoginResponse([property: JsonPropertyName("session")] TimelockLoginSession Session);
    private sealed record EventsResponse([property: JsonPropertyName("events")] IReadOnlyList<TimelockMachineEvent>? Events);
    private sealed record DataResponse<T>(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("data")] T? Data)
        where T : class;
}
