using Microsoft.Data.Sqlite;
using TimeLockApp.Data;
using TimeLockApp.Models;

internal static class InterruptedSessionRecoveryTests
{
    public static IEnumerable<(string Name, Action Run)> All()
    {
        yield return (
            "active session becomes forced logout and user is disabled",
            ActiveSessionBecomesForcedLogoutAndUserIsDisabled);
        yield return (
            "forced logout usage is capped at allowed duration",
            ForcedLogoutUsageIsCappedAtAllowedDuration);
        yield return (
            "completed session remains unchanged during recovery",
            CompletedSessionRemainsUnchangedDuringRecovery);
        yield return (
            "recovery rolls back session when user update fails",
            RecoveryRollsBackSessionWhenUserUpdateFails);
    }

    private static void ActiveSessionBecomesForcedLogoutAndUserIsDisabled()
    {
        using var fixture = new TestDatabase();
        UserRecord user = AddUser(fixture, 401, "interrupted", 180);
        int sessionId = fixture.Service.StartSession(user);
        SetSessionStart(fixture.DatabasePath, sessionId, "2026-08-07 10:00:00");

        int recovered = fixture.Service.RecoverInterruptedSessions(
            new DateTime(2026, 8, 7, 10, 20, 0));

        DatabaseService.SessionRecord session = fixture.Service
            .GetAllSessions()
            .Single(item => item.Id == sessionId);
        UserRecord recoveredUser = fixture.Service
            .GetAllUsers()
            .Single(item => item.Id == user.Id);

        AssertEqual(1, recovered, "Recovered count");
        AssertEqual("forced_logout", session.Status, "Session status");
        AssertEqual("2026-08-07 10:20:00", session.EndTime, "End time");
        AssertEqual(1200, session.UsedSeconds, "Used seconds");
        AssertFalse(recoveredUser.IsActive, "User must be inactive");
        AssertTrue(recoveredUser.IsConsumed, "User must be consumed");
        AssertTrue(
            recoveredUser.DeactivationPending,
            "Sheet deactivation must be pending");
    }

    private static void ForcedLogoutUsageIsCappedAtAllowedDuration()
    {
        using var fixture = new TestDatabase();
        UserRecord user = AddUser(fixture, 402, "capped", 10);
        int sessionId = fixture.Service.StartSession(user);
        SetSessionStart(fixture.DatabasePath, sessionId, "2026-08-07 10:00:00");

        fixture.Service.RecoverInterruptedSessions(
            new DateTime(2026, 8, 7, 11, 0, 0));

        DatabaseService.SessionRecord session = fixture.Service
            .GetAllSessions()
            .Single(item => item.Id == sessionId);

        AssertEqual(600, session.UsedSeconds, "Capped used seconds");
    }

    private static void CompletedSessionRemainsUnchangedDuringRecovery()
    {
        using var fixture = new TestDatabase();
        UserRecord user = AddUser(fixture, 403, "completed", 30);
        int sessionId = fixture.Service.StartSession(user);
        fixture.Service.EndSession(sessionId, 90, "completed");
        DatabaseService.SessionRecord before = fixture.Service
            .GetAllSessions()
            .Single(item => item.Id == sessionId);

        int recovered = fixture.Service.RecoverInterruptedSessions(
            new DateTime(2026, 8, 7, 12, 0, 0));

        DatabaseService.SessionRecord after = fixture.Service
            .GetAllSessions()
            .Single(item => item.Id == sessionId);

        AssertEqual(0, recovered, "Recovered count");
        AssertEqual(before.Status, after.Status, "Status");
        AssertEqual(before.EndTime, after.EndTime, "End time");
        AssertEqual(before.UsedSeconds, after.UsedSeconds, "Used seconds");
    }

    private static void RecoveryRollsBackSessionWhenUserUpdateFails()
    {
        using var fixture = new TestDatabase();
        UserRecord user = AddUser(fixture, 404, "rollback", 30);
        int sessionId = fixture.Service.StartSession(user);
        SetSessionStart(fixture.DatabasePath, sessionId, "2026-08-07 10:00:00");

        ExecuteSql(fixture.DatabasePath, $"""
            CREATE TRIGGER fail_forced_logout_user_update
            BEFORE UPDATE ON users
            WHEN OLD.id = {user.Id}
            BEGIN
                SELECT RAISE(ABORT, 'forced failure');
            END;
            """);

        bool threw = false;

        try
        {
            fixture.Service.RecoverInterruptedSessions(
                new DateTime(2026, 8, 7, 10, 5, 0));
        }
        catch (SqliteException)
        {
            threw = true;
        }

        AssertTrue(threw, "Recovery must surface the database failure");

        DatabaseService.SessionRecord session = fixture.Service
            .GetAllSessions()
            .Single(item => item.Id == sessionId);
        UserRecord unchangedUser = fixture.Service
            .GetAllUsers()
            .Single(item => item.Id == user.Id);

        AssertEqual("active", session.Status, "Rolled-back session status");
        AssertEqual("", session.EndTime, "Rolled-back end time");
        AssertTrue(unchangedUser.IsActive, "User must remain active");
        AssertFalse(unchangedUser.IsConsumed, "User must remain unconsumed");
    }

    private static UserRecord AddUser(
        TestDatabase fixture,
        int externalId,
        string username,
        int allowedMinutes)
    {
        fixture.Service.SynchronizeUsers(new[]
        {
            new GoogleSheetUser
            {
                UserId = externalId,
                Username = username,
                Password = "password",
                AllowedMinutes = allowedMinutes,
                Role = "user",
                IsActive = true
            }
        });

        return fixture.Service.GetAllUsers().Single(user =>
            user.Username == username);
    }

    private static void SetSessionStart(
        string databasePath,
        int sessionId,
        string startTime)
    {
        using var connection = new SqliteConnection(
            $"Data Source={databasePath}");
        connection.Open();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            UPDATE sessions
            SET start_time = $start_time
            WHERE id = $session_id;
            """;
        command.Parameters.AddWithValue("$start_time", startTime);
        command.Parameters.AddWithValue("$session_id", sessionId);
        command.ExecuteNonQuery();
    }

    private static void ExecuteSql(string databasePath, string sql)
    {
        using var connection = new SqliteConnection(
            $"Data Source={databasePath}");
        connection.Open();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertFalse(bool condition, string message)
    {
        AssertTrue(!condition, message);
    }

    private static void AssertEqual<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException(
                $"{label}: expected '{expected}', got '{actual}'.");
        }
    }
}
