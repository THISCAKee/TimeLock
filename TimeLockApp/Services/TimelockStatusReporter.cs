using System.Reflection;

namespace TimeLockApp.Services;

internal sealed class TimelockStatusReporter : IDisposable
{
    private readonly TimelockApiClient _client;
    private readonly TimelockDeviceConfiguration _configuration;
    private readonly Func<string?> _currentUsername;
    private readonly CancellationTokenSource _stop = new();
    private Task? _runTask;

    internal TimelockStatusReporter(
        TimelockApiClient client,
        TimelockDeviceConfiguration configuration,
        Func<string?> currentUsername)
    {
        _client = client;
        _configuration = configuration;
        _currentUsername = currentUsername;
    }

    internal void Start() => _runTask ??= Task.Run(() => RunAsync(_stop.Token));

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using PeriodicTimer timer = new(TimeSpan.FromSeconds(15));
        do
        {
            try
            {
                string? username = _currentUsername();
                string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
                string osVersion = Environment.OSVersion.VersionString;
                TimelockHeartbeat heartbeat = string.IsNullOrWhiteSpace(username)
                    ? TimelockHeartbeat.Online(_configuration.MachineCode, version, osVersion)
                    : TimelockHeartbeat.Active(_configuration.MachineCode, username, version, osVersion);
                await _client.SendHeartbeatAsync(heartbeat, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // A later heartbeat retries automatically; no credentials or payloads are logged.
            }
        }
        while (await timer.WaitForNextTickAsync(cancellationToken));
    }

    public void Dispose()
    {
        _stop.Cancel();
        try { _runTask?.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
        _stop.Dispose();
    }
}
