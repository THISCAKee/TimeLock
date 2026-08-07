using Microsoft.Data.Sqlite;
using TimeLockApp.Data;
using TimeLockApp.Models;

var tests = new (string Name, Action Run)[]
{
    (
        "empty sheet removes consumed user and preserves history and admin",
        EmptySheetRemovesConsumedUserAndPreservesHistoryAndAdmin),
    (
        "non-empty sheet removes consumed missing user and keeps present user",
        NonEmptySheetRemovesConsumedMissingUserAndKeepsPresentUser),
    (
        "legacy session schema migrates and preserves history",
        LegacySessionSchemaMigratesAndPreservesHistory)
};

tests = tests
    .Concat(LockAndWarningTests.All())
    .Concat(InterruptedSessionRecoveryTests.All())
    .ToArray();

int failures = 0;

foreach ((string name, Action run) in tests)
{
    try
    {
        run();
        Console.WriteLine($"PASS: {name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.Error.WriteLine($"FAIL: {name}");
        Console.Error.WriteLine(ex.Message);
    }
}

return failures == 0 ? 0 : 1;

static void EmptySheetRemovesConsumedUserAndPreservesHistoryAndAdmin()
{
    using var testDatabase = new TestDatabase();
    DatabaseService database = testDatabase.Service;

    GoogleSheetUser removedUser = SheetUser(101, "removed");
    database.SynchronizeUsers(new[] { removedUser });

    UserRecord localUser = RequireUser(database, "removed");
    int sessionId = database.StartSession(localUser);
    database.EndSessionAndDeactivateUser(
        sessionId,
        localUser.Id,
        30,
        "completed");

    database.SynchronizeUsers(Array.Empty<GoogleSheetUser>());

    AssertFalse(
        database.GetAllUsers().Any(user => user.Username == "removed"),
        "Consumed user missing from an empty Sheet must be removed.");
    AssertTrue(
        database.GetAllUsers().Any(user =>
            user.Username == "admin" && user.IsLocalOnly),
        "Local-only admin must remain.");
    AssertTrue(
        database.GetAllSessions().Any(session =>
            session.Username == "removed" && session.Id == sessionId),
        "Session history must remain after user deletion.");
}

static void NonEmptySheetRemovesConsumedMissingUserAndKeepsPresentUser()
{
    using var testDatabase = new TestDatabase();
    DatabaseService database = testDatabase.Service;

    GoogleSheetUser removedUser = SheetUser(201, "removed");
    GoogleSheetUser presentUser = SheetUser(202, "present");
    database.SynchronizeUsers(new[] { removedUser, presentUser });

    UserRecord localUser = RequireUser(database, "removed");
    int sessionId = database.StartSession(localUser);
    database.EndSessionAndDeactivateUser(
        sessionId,
        localUser.Id,
        45,
        "completed");

    database.SynchronizeUsers(new[] { presentUser });

    IReadOnlyList<UserRecord> users = database.GetAllUsers();
    AssertFalse(
        users.Any(user => user.Username == "removed"),
        "Consumed user absent from a non-empty Sheet must be removed.");
    AssertTrue(
        users.Any(user =>
            user.Username == "present" &&
            user.ExternalUserId == presentUser.UserId),
        "User still present in the Sheet must remain.");
}

static void LegacySessionSchemaMigratesAndPreservesHistory()
{
    using var testDatabase = new TestDatabase(initialize: false);

    using (var connection = new SqliteConnection(
               $"Data Source={testDatabase.DatabasePath}"))
    {
        connection.Open();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
        CREATE TABLE users (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            username TEXT NOT NULL UNIQUE,
            password TEXT NOT NULL,
            allowed_minutes INTEGER NOT NULL,
            role TEXT NOT NULL DEFAULT 'user',
            external_user_id INTEGER,
            is_active INTEGER NOT NULL DEFAULT 1,
            is_local_only INTEGER NOT NULL DEFAULT 0,
            is_consumed INTEGER NOT NULL DEFAULT 0,
            deactivation_pending INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE sessions (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            user_id INTEGER NOT NULL,
            username TEXT NOT NULL,
            start_time TEXT NOT NULL,
            end_time TEXT,
            allowed_minutes INTEGER NOT NULL,
            used_seconds INTEGER DEFAULT 0,
            status TEXT NOT NULL DEFAULT 'active',
            FOREIGN KEY (user_id) REFERENCES users(id)
        );

        INSERT INTO users (
            id, external_user_id, username, password,
            allowed_minutes, role, is_active, is_local_only,
            is_consumed, deactivation_pending)
        VALUES (
            1, 301, 'legacy', 'password', 10, 'user',
            0, 0, 1, 0);

        INSERT INTO sessions (
            id, user_id, username, start_time, end_time,
            allowed_minutes, used_seconds, status)
        VALUES (
            1, 1, 'legacy', '2026-08-07 10:00:00',
            '2026-08-07 10:05:00', 10, 300, 'completed');
        """;
        command.ExecuteNonQuery();
    }

    testDatabase.Service.InitializeDatabase();
    testDatabase.Service.SynchronizeUsers(
        Array.Empty<GoogleSheetUser>());

    DatabaseService.SessionRecord session =
        testDatabase.Service.GetAllSessions().Single();

    AssertTrue(
        session.UserId is null,
        "Migrated history must clear its deleted user reference.");
    AssertTrue(
        session.Username == "legacy" &&
        session.UsedSeconds == 300 &&
        session.Status == "completed",
        "Migration must preserve session display fields.");
}

static GoogleSheetUser SheetUser(int id, string username)
{
    return new GoogleSheetUser
    {
        UserId = id,
        Username = username,
        Password = "password",
        AllowedMinutes = 10,
        Role = "user",
        IsActive = true
    };
}

static UserRecord RequireUser(
    DatabaseService database,
    string username)
{
    return database.GetAllUsers().Single(user =>
        user.Username == username);
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertFalse(bool condition, string message)
{
    AssertTrue(!condition, message);
}

sealed class TestDatabase : IDisposable
{
    private readonly string _directoryPath;

    public TestDatabase(bool initialize = true)
    {
        _directoryPath = Path.Combine(
            Path.GetTempPath(),
            "TimeLockApp.Tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_directoryPath);

        DatabasePath = Path.Combine(
            _directoryPath,
            "timelock.db");
        Service = new DatabaseService(DatabasePath);

        if (initialize)
        {
            Service.InitializeDatabase();
        }
    }

    public string DatabasePath { get; }

    public DatabaseService Service { get; }

    public void Dispose()
    {
        string resolvedDirectory = Path.GetFullPath(_directoryPath);
        string resolvedTestRoot = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "TimeLockApp.Tests"));

        if (!resolvedDirectory.StartsWith(
                resolvedTestRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Refusing to remove a directory outside the test root.");
        }

        SqliteConnection.ClearAllPools();
        Directory.Delete(resolvedDirectory, recursive: true);
    }
}
