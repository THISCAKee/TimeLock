using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace TimeLockApp.Services;

public sealed record TimelockDeviceConfiguration(
    string MachineCode,
    string DeviceToken,
    string BackendUrl,
    PasswordVerifier LocalAdminVerifier)
{
    public static TimelockDeviceConfiguration Create(
        string machineCode,
        string deviceToken,
        string backendUrl,
        PasswordVerifier? localAdminVerifier = null)
    {
        machineCode = machineCode?.Trim().ToUpperInvariant() ?? "";
        deviceToken = deviceToken?.Trim() ?? "";
        backendUrl = backendUrl?.Trim().TrimEnd('/') ?? "";
        if (machineCode.Length == 0 || deviceToken.Length == 0 ||
            !Uri.TryCreate(backendUrl, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Machine code, device token and HTTPS backend URL are required.");
        }

        return new TimelockDeviceConfiguration(
            machineCode,
            deviceToken,
            backendUrl,
            localAdminVerifier ?? PasswordVerifier.Create(Guid.NewGuid().ToString("N")));
    }
}

public sealed record PasswordVerifier(
    [property: JsonPropertyName("algorithm")] string Algorithm,
    [property: JsonPropertyName("iterations")] int Iterations,
    [property: JsonPropertyName("salt")] string Salt,
    [property: JsonPropertyName("hash")] string Hash)
{
    public static PasswordVerifier Create(string password, int iterations = 600_000)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            32);
        return new PasswordVerifier(
            "pbkdf2-sha256",
            iterations,
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public bool Verify(string password)
    {
        if (Algorithm != "pbkdf2-sha256" || Iterations <= 0) return false;
        try
        {
            byte[] expected = Convert.FromBase64String(Hash);
            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
                password,
                Convert.FromBase64String(Salt),
                Iterations,
                HashAlgorithmName.SHA256,
                expected.Length);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public sealed record TimelockHeartbeat(
    [property: JsonPropertyName("machineCode")] string MachineCode,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("sessionStatus")] string SessionStatus,
    [property: JsonPropertyName("appVersion")] string AppVersion,
    [property: JsonPropertyName("osVersion")] string OsVersion,
    [property: JsonPropertyName("reportedAt")] DateTimeOffset ReportedAt)
{
    public static TimelockHeartbeat Online(string machineCode, string appVersion, string osVersion) =>
        new(machineCode, null, "logged_out", appVersion, osVersion, DateTimeOffset.UtcNow);

    public static TimelockHeartbeat Active(string machineCode, string username, string appVersion, string osVersion) =>
        new(machineCode, username, "logged_in", appVersion, osVersion, DateTimeOffset.UtcNow);
}

public sealed record TimelockOfflineAccount(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("allowedMinutes")] int AllowedMinutes,
    [property: JsonPropertyName("isActive")] bool IsActive,
    [property: JsonPropertyName("verifier")] PasswordVerifier Verifier,
    [property: JsonPropertyName("issuedAt")] DateTimeOffset IssuedAt,
    [property: JsonPropertyName("expiresAt")] DateTimeOffset ExpiresAt);

public sealed record TimelockLoginSession(
    [property: JsonPropertyName("sessionId")] string SessionId,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("allowedMinutes")] int AllowedMinutes,
    [property: JsonPropertyName("startedAt")] DateTimeOffset StartedAt,
    bool IsOffline = false);
