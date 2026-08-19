using System.IO;

namespace TimeLockApp.Services;

internal sealed record PendingTimelockSession(
    TimelockLoginSession Session,
    int UsedSeconds,
    string Status);

internal sealed class TimelockPendingSessionStore
{
    private readonly string _path;
    private readonly ProtectedFileStore _store = new();

    internal TimelockPendingSessionStore(string? dataDirectory = null)
    {
        dataDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "TimeLockApp");
        _path = Path.Combine(dataDirectory, "pending-sessions.bin");
    }

    internal IReadOnlyList<PendingTimelockSession> Load() =>
        _store.Load<List<PendingTimelockSession>>(_path) ?? [];

    internal void Add(PendingTimelockSession pending)
    {
        List<PendingTimelockSession> all = Load().ToList();
        all.RemoveAll(item => item.Session.SessionId == pending.Session.SessionId);
        all.Add(pending);
        _store.Save(_path, all);
    }

    internal void Remove(string sessionId)
    {
        List<PendingTimelockSession> all = Load()
            .Where(item => item.Session.SessionId != sessionId)
            .ToList();
        _store.Save(_path, all);
    }
}
