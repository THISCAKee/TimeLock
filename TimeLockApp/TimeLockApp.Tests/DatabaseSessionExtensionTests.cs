using Microsoft.Data.Sqlite;
using TimeLockApp.Data;

internal static class DatabaseSessionExtensionTests
{
    public static IEnumerable<(string Name, Action Run)> All()
    {
        yield return ("session extension updates active session allowance", UpdatesActiveSessionAllowance);
    }

    private static void UpdatesActiveSessionAllowance()
    {
        using TestDatabase testDatabase = new();
        int sessionId = testDatabase.Service.StartSession("booking_0001", 180);

        testDatabase.Service.UpdateSessionAllowance(sessionId, 360);

        DatabaseService.SessionRecord session =
            testDatabase.Service.GetAllSessions().Single(item => item.Id == sessionId);
        AssertTrue(session.AllowedMinutes == 360, "Local session accounting must use the server allowance.");
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class TestDatabase : IDisposable
    {
        private readonly string _directory;

        internal TestDatabase()
        {
            _directory = Path.Combine(
                Path.GetTempPath(),
                "TimeLockApp.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            Service = new DatabaseService(Path.Combine(_directory, "timelock.db"));
            Service.InitializeDatabase();
        }

        internal DatabaseService Service { get; }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            string resolved = Path.GetFullPath(_directory);
            string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "TimeLockApp.Tests"));
            if (!resolved.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Refusing to remove a directory outside the test root.");
            }
            Directory.Delete(resolved, recursive: true);
        }
    }
}
