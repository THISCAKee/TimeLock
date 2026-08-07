# Delete Google Sheet Users from Admin Panel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove every sheet-managed user missing from the latest successful Google Sheet read from the Admin Panel while preserving local-only administrators and session history.

**Architecture:** Keep the existing `GoogleSheetsUserService` → `UserSyncService` → `DatabaseService.SynchronizeUsers` flow. Add a dependency-free database regression-test executable that uses isolated temporary SQLite files, remove the `is_consumed` exclusion from both missing-user deletion branches, and migrate `sessions.user_id` to a nullable `ON DELETE SET NULL` foreign key so history survives physical user deletion.

**Tech Stack:** C# 14, .NET 10, WPF, Microsoft.Data.Sqlite 10.0.10, dependency-free console regression tests

## Global Constraints

- Preserve all rows and display fields in the `sessions` table; only `user_id` becomes null when its user is deleted.
- Never delete a row where `is_local_only = 1` during Google Sheet synchronization.
- Apply deletion only after a complete, valid Sheet result reaches `DatabaseService.SynchronizeUsers`.
- Keep upserts and missing-user deletion in the existing SQLite transaction.
- Do not change the Google Sheet schema, synchronization triggers, deactivation flow, or Admin Panel layout.
- Do not add archive or soft-delete behavior.

---

## File Structure

- `TimeLockApp.Tests/TimeLockApp.Tests.csproj`: dependency-free executable test project referencing the application project.
- `TimeLockApp.Tests/Program.cs`: isolated SQLite integration tests and minimal assertion runner.
- `.gitignore`: excludes build outputs created by the nested test project.
- `TimeLockApp.csproj`: excludes nested test sources from the WPF application's default recursive compile glob.
- `AssemblyInfo.cs`: grants the test assembly access to the database-path constructor without expanding the production API.
- `data/DatabaseService.cs`: accepts an internal test database path, migrates the session foreign key, reads nullable historical user IDs, and deletes missing sheet-managed users regardless of `is_consumed`.

### Task 1: Missing Sheet User Deletion Regression

**Files:**
- Create: `TimeLockApp.Tests/TimeLockApp.Tests.csproj`
- Create: `TimeLockApp.Tests/Program.cs`
- Modify: `.gitignore`
- Modify: `TimeLockApp.csproj`
- Modify: `AssemblyInfo.cs`
- Modify: `data/DatabaseService.cs:13-20`
- Modify: `data/DatabaseService.cs:503-549`

**Interfaces:**
- Consumes: `DatabaseService.InitializeDatabase()`, `SynchronizeUsers(IReadOnlyList<GoogleSheetUser>)`, `GetAllUsers()`, `StartSession(UserRecord)`, `EndSessionAndDeactivateUser(int, int, int, string)`, and `GetAllSessions()`.
- Produces: internal `DatabaseService(string databasePath)` for isolated tests; nullable `SessionRecord.UserId`; a transactional legacy-session-schema migration; synchronization behavior that deletes all missing rows with `is_local_only = 0`.

- [x] **Step 1: Add the isolated test-project boundary**

Create `TimeLockApp.Tests/TimeLockApp.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\TimeLockApp.csproj" />
  </ItemGroup>
</Project>
```

Add this item group to `TimeLockApp.csproj` so the main WPF project does not compile the nested test runner through its default recursive source glob:

```xml
<ItemGroup>
  <Compile Remove="TimeLockApp.Tests\**\*.cs" />
</ItemGroup>
```

Add the following to `AssemblyInfo.cs`:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("TimeLockApp.Tests")]
```

Replace the current constructor in `data/DatabaseService.cs` with an app constructor that delegates to an internal path-based constructor:

```csharp
public DatabaseService()
    : this(Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory,
        "timelock.db"))
{
}

internal DatabaseService(string databasePath)
{
    if (string.IsNullOrWhiteSpace(databasePath))
    {
        throw new ArgumentException(
            "Database path is required.",
            nameof(databasePath));
    }

    _connectionString =
        new SqliteConnectionStringBuilder
        {
            DataSource = databasePath
        }.ToString();
}
```

- [x] **Step 2: Write regression tests that exercise both SQL branches**

Create `TimeLockApp.Tests/Program.cs` with a small runner and real temporary SQLite databases:

```csharp
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
        VALUES (1, 301, 'legacy', 'password', 10, 'user', 0, 0, 1, 0);

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

static UserRecord RequireUser(DatabaseService database, string username)
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

        Directory.Delete(resolvedDirectory, recursive: true);
    }
}
```

The production mutation caught by both tests is restoring either `AND is_consumed = 0` predicate in `DeleteMissingSheetUsers`. The expected user names and IDs are literal fixtures and all database behavior is real; no Google API mock is required because the contract under test begins after a successful Sheet read.

- [x] **Step 3: Run the regression tests and verify RED**

Run:

```powershell
dotnet run --project TimeLockApp.Tests\TimeLockApp.Tests.csproj
```

Expected before the deletion predicate change: exit code `1`; the first two tests print `FAIL` because the consumed `removed` user remains. After removing those predicates but before the schema migration, all deletion tests fail with SQLite foreign-key errors. These failures demonstrate both layers of the regression.

- [x] **Step 4: Implement the minimal deletion change**

In both branches of `DeleteMissingSheetUsers` in `data/DatabaseService.cs`, remove only the consumed-user exclusion.

Empty-Sheet SQL:

```csharp
command.CommandText = """
DELETE FROM users
WHERE is_local_only = 0;
""";
```

Non-empty-Sheet SQL:

```csharp
command.CommandText = $"""
DELETE FROM users
WHERE is_local_only = 0
  AND external_user_id NOT IN ({parameterList});
""";
```

Then update new database creation so `sessions.user_id` is nullable and the foreign key ends in `ON DELETE SET NULL`. Add `EnsureSessionsSchemaSupportsUserDeletion(SqliteConnection)` after `CREATE TABLE IF NOT EXISTS sessions`; it must inspect `PRAGMA table_info(sessions)` and `PRAGMA foreign_key_list(sessions)`, and when legacy constraints are detected, transactionally create `sessions_migrated`, copy all eight columns, drop `sessions`, and rename the migrated table. Finally, read `SessionRecord.UserId` with `reader.IsDBNull(1) ? null : reader.GetInt32(1)` and change the property type to `int?`.

Do not alter pending-deactivation processing or Admin Panel refresh logic.

- [x] **Step 5: Run focused tests and verify GREEN**

Run:

```powershell
dotnet run --project TimeLockApp.Tests\TimeLockApp.Tests.csproj
```

Expected: exit code `0` with three `PASS` lines and no `FAIL` output. This verifies new databases, migration of legacy databases, user deletion, and preservation of session display fields.

- [x] **Step 6: Verify the application build and repository diff**

Run:

```powershell
dotnet build TimeLockApp.csproj --no-restore
git diff --check
git diff -- .gitignore TimeLockApp.csproj AssemblyInfo.cs data/DatabaseService.cs TimeLockApp.Tests
```

Expected: build succeeds with zero errors; `git diff --check` reports no whitespace errors; the scoped diff contains only the test seam, regression tests, and the two removed `is_consumed` predicates. Existing unrelated generated-file changes must remain untouched.

- [ ] **Step 7: Commit the tested implementation**

```powershell
git add -- .gitignore TimeLockApp.csproj AssemblyInfo.cs data/DatabaseService.cs TimeLockApp.Tests/TimeLockApp.Tests.csproj TimeLockApp.Tests/Program.cs docs/superpowers/specs/2026-08-07-delete-sheet-users-from-admin-design.md docs/superpowers/plans/2026-08-07-delete-sheet-users-from-admin.md
git commit -m "fix: remove deleted sheet users from admin"
```

Expected: one commit containing only the six implementation/test files and two approved documentation files. Do not stage existing changes under the root `bin`, root `obj`, the WebView profile, or the local database.
