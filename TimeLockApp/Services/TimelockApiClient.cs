using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TimeLockApp.Services;

internal sealed class TimelockApiException(string code) : Exception(code)
{
    internal string Code { get; } = code;
}

internal sealed class TimelockApiClient : IDisposable
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

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            await ThrowApiError(response, cancellationToken);
        }
        T? value = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
        return value ?? throw new TimelockApiException("INVALID_SERVER_RESPONSE");
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
            throw new TimelockApiException(error?.Code ?? "SERVER_REQUEST_FAILED");
        }
        catch (JsonException)
        {
            throw new TimelockApiException("SERVER_REQUEST_FAILED");
        }
    }

    public void Dispose() => _http.Dispose();

    private sealed record ApiError([property: JsonPropertyName("code")] string Code);
    private sealed record SyncResponse([property: JsonPropertyName("accounts")] IReadOnlyList<TimelockOfflineAccount> Accounts);
    private sealed record LoginResponse([property: JsonPropertyName("session")] TimelockLoginSession Session);
}
