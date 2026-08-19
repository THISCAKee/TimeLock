using System.IO;

namespace TimeLockApp.Services;

internal sealed record TimelockOfflineCacheEnvelope(
    DateTimeOffset LastServerTime,
    IReadOnlyList<TimelockOfflineAccount> Accounts);

internal sealed class TimelockOfflineCache
{
    private static readonly TimeSpan ClockTolerance = TimeSpan.FromMinutes(5);
    private readonly string _path;
    private readonly ProtectedFileStore _store = new();

    internal TimelockOfflineCache(string? dataDirectory = null)
    {
        dataDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "TimeLockApp");
        _path = Path.Combine(dataDirectory, "offline-cache.bin");
    }

    internal void Save(IReadOnlyList<TimelockOfflineAccount> accounts, DateTimeOffset serverTime) =>
        _store.Save(_path, new TimelockOfflineCacheEnvelope(serverTime, accounts));

    internal TimelockOfflineAccount? Authenticate(string username, string password, DateTimeOffset now)
    {
        TimelockOfflineCacheEnvelope? cache = _store.Load<TimelockOfflineCacheEnvelope>(_path);
        if (cache is null || now + ClockTolerance < cache.LastServerTime) return null;
        TimelockOfflineAccount? account = cache.Accounts.SingleOrDefault(item =>
            string.Equals(item.Username, username, StringComparison.OrdinalIgnoreCase));
        if (account is null || !account.IsActive || now > account.ExpiresAt) return null;
        if (!account.Verifier.Verify(password)) return null;

        IReadOnlyList<TimelockOfflineAccount> consumed = cache.Accounts
            .Select(item => item.Id == account.Id ? item with { IsActive = false } : item)
            .ToArray();
        _store.Save(_path, new TimelockOfflineCacheEnvelope(
            now > cache.LastServerTime ? now : cache.LastServerTime,
            consumed));
        return account;
    }
}
