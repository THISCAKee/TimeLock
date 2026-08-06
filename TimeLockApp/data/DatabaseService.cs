using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TimeLockApp.Models;

namespace TimeLockApp.Data;

public class DatabaseService
{

    private readonly string _connectionString;


    public DatabaseService()
    {
        string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "timelock.db");
        _connectionString = $"Data Source={dbPath}";
    }

    public void SynchronizeUsers(
    IReadOnlyList<GoogleSheetUser> sheetUsers)
    {
        ValidateSheetUsers(sheetUsers);

        using var connection =
            new SqliteConnection(_connectionString);

        connection.Open();

        using SqliteTransaction transaction =
            connection.BeginTransaction();

        try
        {
            foreach (GoogleSheetUser user in sheetUsers)
            {
                UpsertSheetUser(
                    connection,
                    transaction,
                    user);
            }

            DeleteMissingSheetUsers(
                connection,
                transaction,
                sheetUsers);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    //ล้างประวัติการใช้งานทั้งหมด
    public void ClearAllSessions()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        string sql = @"
        DELETE FROM sessions;
        DELETE FROM sqlite_sequence WHERE name = 'sessions';
    ";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
    public int StartSession(UserRecord user)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        string sql = @"
        INSERT INTO sessions (
            user_id,
            username,
            start_time,
            allowed_minutes,
            status
        )
        VALUES (
            $user_id,
            $username,
            $start_time,
            $allowed_minutes,
            'active'
        );

        SELECT last_insert_rowid();
    ";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$user_id", user.Id);
        command.Parameters.AddWithValue("$username", user.Username);
        command.Parameters.AddWithValue("$start_time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        command.Parameters.AddWithValue("$allowed_minutes", user.AllowedMinutes);

        long sessionId = (long)command.ExecuteScalar()!;
        return (int)sessionId;
    }

    public void EndSession(int sessionId, int usedSeconds, string status)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        string sql = @"
        UPDATE sessions
        SET end_time = $end_time,
            used_seconds = $used_seconds,
            status = $status
        WHERE id = $id;
    ";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$id", sessionId);
        command.Parameters.AddWithValue("$end_time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        command.Parameters.AddWithValue("$used_seconds", usedSeconds);
        command.Parameters.AddWithValue("$status", status);

        command.ExecuteNonQuery();
    }

    public void EndSessionAndDeactivateUser(
        int sessionId,
        int userId,
        int usedSeconds,
        string status)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using SqliteTransaction transaction =
            connection.BeginTransaction();

        try
        {
            const string endSessionSql = """
            UPDATE sessions
            SET end_time = $end_time,
                used_seconds = $used_seconds,
                status = $status
            WHERE id = $session_id;
            """;

            using (SqliteCommand sessionCommand =
                   connection.CreateCommand())
            {
                sessionCommand.Transaction = transaction;
                sessionCommand.CommandText = endSessionSql;
                sessionCommand.Parameters.AddWithValue(
                    "$session_id",
                    sessionId);
                sessionCommand.Parameters.AddWithValue(
                    "$end_time",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sessionCommand.Parameters.AddWithValue(
                    "$used_seconds",
                    usedSeconds);
                sessionCommand.Parameters.AddWithValue(
                    "$status",
                    status);

                if (sessionCommand.ExecuteNonQuery() != 1)
                {
                    throw new InvalidOperationException(
                        "ไม่พบ session ที่ต้องการสิ้นสุด");
                }
            }

            const string deactivateUserSql = """
            UPDATE users
            SET is_active = 0,
                is_consumed = 1,
                deactivation_pending = CASE
                    WHEN external_user_id IS NULL THEN 0
                    ELSE 1
                END
            WHERE id = $user_id
              AND is_local_only = 0;
            """;

            using (SqliteCommand userCommand =
                   connection.CreateCommand())
            {
                userCommand.Transaction = transaction;
                userCommand.CommandText = deactivateUserSql;
                userCommand.Parameters.AddWithValue(
                    "$user_id",
                    userId);

                if (userCommand.ExecuteNonQuery() != 1)
                {
                    throw new InvalidOperationException(
                        "ไม่สามารถตัดสิทธิ์ผู้ใช้ของ session นี้ได้");
                }
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public List<PendingUserDeactivation>
        GetPendingUserDeactivations()
    {
        var pendingUsers =
            new List<PendingUserDeactivation>();

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        const string sql = """
        SELECT id, external_user_id
        FROM users
        WHERE deactivation_pending = 1
          AND external_user_id IS NOT NULL
          AND is_consumed = 1;
        """;

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            pendingUsers.Add(new PendingUserDeactivation
            {
                LocalUserId = reader.GetInt32(0),
                ExternalUserId = reader.GetInt32(1)
            });
        }

        return pendingUsers;
    }

    public void MarkUserDeactivationSynchronized(int userId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        const string sql = """
        UPDATE users
        SET deactivation_pending = 0
        WHERE id = $user_id
          AND is_consumed = 1;
        """;

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$user_id", userId);
        command.ExecuteNonQuery();
    }

    public List<SessionRecord> GetAllSessions()
    {
        List<SessionRecord> sessions = new();

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        string sql = @"
        SELECT id,
               user_id,
               username,
               start_time,
               end_time,
               allowed_minutes,
               used_seconds,
               status
        FROM sessions
        ORDER BY id DESC;
    ";

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            sessions.Add(new SessionRecord
            {
                Id = reader.GetInt32(0),
                UserId = reader.GetInt32(1),
                Username = reader.GetString(2),
                StartTime = reader.GetString(3),
                EndTime = reader.IsDBNull(4) ? "" : reader.GetString(4),
                AllowedMinutes = reader.GetInt32(5),
                UsedSeconds = reader.GetInt32(6),
                Status = reader.GetString(7)
            });
        }

        return sessions;
    }

    public void InitializeDatabase()
    {

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        string createUsersTableSql = @"
        CREATE TABLE IF NOT EXISTS users (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            username TEXT NOT NULL UNIQUE,
            password TEXT NOT NULL,
            allowed_minutes INTEGER NOT NULL,
            role TEXT NOT NULL DEFAULT 'user'
        );
    ";

        using var createCommand = connection.CreateCommand();
        createCommand.CommandText = createUsersTableSql;
        createCommand.ExecuteNonQuery();

        // เพิ่มตรงนี้
        AddColumnIfMissing(
            connection,
            "users",
            "external_user_id",
            "INTEGER");

        AddColumnIfMissing(
            connection,
            "users",
            "is_active",
            "INTEGER NOT NULL DEFAULT 1");

        AddColumnIfMissing(
            connection,
            "users",
            "is_local_only",
            "INTEGER NOT NULL DEFAULT 0");

        AddColumnIfMissing(
            connection,
            "users",
            "is_consumed",
            "INTEGER NOT NULL DEFAULT 0");

        AddColumnIfMissing(
            connection,
            "users",
            "deactivation_pending",
            "INTEGER NOT NULL DEFAULT 0");

        // แล้วค่อย seed user เดิม
        SeedDefaultUsers(connection);

        // ทำให้ admin เป็น local-only
        MarkEmergencyAdminAsLocalOnly(connection);

        string createSessionsTableSql = @"
        CREATE TABLE IF NOT EXISTS sessions (
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
    ";

        using var createSessionsCommand = connection.CreateCommand();
        createSessionsCommand.CommandText = createSessionsTableSql;
        createSessionsCommand.ExecuteNonQuery();

    }
    //เพิ่ม validation ป้องกันข้อมูลซ้ำ
    private static void ValidateSheetUsers(
    IReadOnlyList<GoogleSheetUser> users)
    {
        int? duplicateUserId = users
            .GroupBy(user => user.UserId)
            .Where(group => group.Count() > 1)
            .Select(group => (int?)group.Key)
            .FirstOrDefault();

        if (duplicateUserId.HasValue)
        {
            throw new InvalidOperationException(
                $"พบ UserId ซ้ำใน Google Sheet: {duplicateUserId.Value}");
        }

        string? duplicateUsername = users
            .GroupBy(
                user => user.Username,
                StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(duplicateUsername))
        {
            throw new InvalidOperationException(
                $"พบ Username ซ้ำใน Google Sheet: {duplicateUsername}");
        }

        GoogleSheetUser? adminUser = users
            .FirstOrDefault(user =>
                string.Equals(
                    user.Username,
                    "admin",
                    StringComparison.OrdinalIgnoreCase));

        if (adminUser != null)
        {
            throw new InvalidOperationException(
                "ไม่ต้องเพิ่ม admin ลง Google Sheet เพราะ admin ถูกเก็บในเครื่องอยู่แล้ว");
        }
    }
    //method Insert/Update user จาก Sheet
    private static void UpsertSheetUser(
    SqliteConnection connection,
    SqliteTransaction transaction,
    GoogleSheetUser user)
    {
        const string sql = """
        INSERT INTO users (
            external_user_id,
            username,
            password,
            allowed_minutes,
            role,
            is_active,
            is_local_only,
            is_consumed,
            deactivation_pending
        )
        VALUES (
            $external_user_id,
            $username,
            $password,
            $allowed_minutes,
            $role,
            $is_active,
            0,
            0,
            0
        )
        ON CONFLICT(username) DO UPDATE SET
            external_user_id = excluded.external_user_id,
            password = excluded.password,
            allowed_minutes = excluded.allowed_minutes,
            role = excluded.role,
            is_active = CASE
                WHEN users.is_consumed = 1 THEN 0
                ELSE excluded.is_active
            END
        WHERE users.is_local_only = 0;
        """;

        using var command = connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText = sql;

        command.Parameters.AddWithValue(
            "$external_user_id",
            user.UserId);

        command.Parameters.AddWithValue(
            "$username",
            user.Username);

        command.Parameters.AddWithValue(
            "$password",
            user.Password);

        command.Parameters.AddWithValue(
            "$allowed_minutes",
            user.AllowedMinutes);

        command.Parameters.AddWithValue(
            "$role",
            user.Role);

        command.Parameters.AddWithValue(
            "$is_active",
            user.IsActive ? 1 : 0);

        command.ExecuteNonQuery();
    }
    //method ลบ user ที่หายจาก Sheet
    private static void DeleteMissingSheetUsers(
    SqliteConnection connection,
    SqliteTransaction transaction,
    IReadOnlyList<GoogleSheetUser> sheetUsers)
    {
        using var command = connection.CreateCommand();

        command.Transaction = transaction;

        if (sheetUsers.Count == 0)
        {
            command.CommandText = """
            DELETE FROM users
            WHERE is_local_only = 0
              AND is_consumed = 0;
            """;

            command.ExecuteNonQuery();
            return;
        }

        var parameterNames = new List<string>();

        for (int index = 0;
             index < sheetUsers.Count;
             index++)
        {
            string parameterName = $"$user_id_{index}";

            parameterNames.Add(parameterName);

            command.Parameters.AddWithValue(
                parameterName,
                sheetUsers[index].UserId);
        }

        string parameterList =
            string.Join(", ", parameterNames);

        command.CommandText = $"""
        DELETE FROM users
        WHERE is_local_only = 0
          AND is_consumed = 0
          AND external_user_id NOT IN ({parameterList});
        """;

        command.ExecuteNonQuery();
    }

    private static bool ColumnExists(
    SqliteConnection connection,
    string tableName,
    string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";

        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            string existingColumnName = reader.GetString(1);

            if (string.Equals(
                existingColumnName,
                columnName,
                StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddColumnIfMissing(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string definition)
    {
        if (ColumnExists(connection, tableName, columnName))
        {
            return;
        }

        using var command = connection.CreateCommand();

        command.CommandText =
            $"ALTER TABLE {tableName} " +
            $"ADD COLUMN {columnName} {definition};";

        command.ExecuteNonQuery();
    }

    private static void MarkEmergencyAdminAsLocalOnly(
    SqliteConnection connection)
    {
        const string sql = """
        UPDATE users
        SET is_local_only = 1,
            is_active = 1
        WHERE username = 'admin'
          AND role = 'admin';
        """;

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }


    private static void SeedDefaultUsers(
     SqliteConnection connection)
    {
        const string checkSql = """
        SELECT COUNT(*)
        FROM users
        WHERE username = 'admin';
        """;

        using var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = checkSql;

        long adminCount =
            Convert.ToInt64(checkCommand.ExecuteScalar());

        if (adminCount > 0)
        {
            return;
        }

        const string insertSql = """
        INSERT INTO users (
            username,
            password,
            allowed_minutes,
            role,
            is_active,
            is_local_only
        )
        VALUES (
            'admin',
            'admin123',
            0,
            'admin',
            1,
            1
        );
        """;

        using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = insertSql;
        insertCommand.ExecuteNonQuery();
    }

    public UserRecord? GetUserByUsernameAndPassword(
    string username,
    string password)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        const string sql = """
        SELECT id,
               external_user_id,
               username,
               password,
               allowed_minutes,
               role,
               is_active,
               is_local_only,
               is_consumed,
               deactivation_pending
        FROM users
        WHERE username = $username
          AND password = $password
          AND is_active = 1
          AND is_consumed = 0
        LIMIT 1;
        """;

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        command.Parameters.AddWithValue(
            "$username",
            username);

        command.Parameters.AddWithValue(
            "$password",
            password);

        using var reader = command.ExecuteReader();

        if (!reader.Read())
        {
            return null;
        }

        return new UserRecord
        {
            Id = reader.GetInt32(0),

            ExternalUserId = reader.IsDBNull(1)
                ? null
                : reader.GetInt32(1),

            Username = reader.GetString(2),
            Password = reader.GetString(3),
            AllowedMinutes = reader.GetInt32(4),
            Role = reader.GetString(5),
            IsActive = reader.GetInt32(6) == 1,
            IsLocalOnly = reader.GetInt32(7) == 1,
            IsConsumed = reader.GetInt32(8) == 1,
            DeactivationPending = reader.GetInt32(9) == 1
        };
    }
    public class SessionRecord
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string StartTime { get; set; } = "";
        public string EndTime { get; set; } = "";
        public int AllowedMinutes { get; set; }
        public int UsedSeconds { get; set; }
        public string Status { get; set; } = "";

        public string UsedTimeDisplay
        {
            get
            {
                int minutes = UsedSeconds / 60;
                int seconds = UsedSeconds % 60;
                return $"{minutes:00}:{seconds:00}";
            }
        }
    }

    public List<UserRecord> GetAllUsers()
    {
        List<UserRecord> users = new();

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        const string sql = """
        SELECT id,
               external_user_id,
               username,
               password,
               allowed_minutes,
               role,
               is_active,
               is_local_only,
               is_consumed,
               deactivation_pending
        FROM users
        ORDER BY id;
        """;

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            users.Add(new UserRecord
            {
                Id = reader.GetInt32(0),

                ExternalUserId = reader.IsDBNull(1)
                    ? null
                    : reader.GetInt32(1),

                Username = reader.GetString(2),
                Password = reader.GetString(3),
                AllowedMinutes = reader.GetInt32(4),
                Role = reader.GetString(5),
                IsActive = reader.GetInt32(6) == 1,
                IsLocalOnly = reader.GetInt32(7) == 1,
                IsConsumed = reader.GetInt32(8) == 1,
                DeactivationPending = reader.GetInt32(9) == 1
            });
        }

        return users;
    }
    public bool AddUser(
     string username,
     string password,
     int allowedMinutes,
     string role)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            const string sql = """
            INSERT INTO users (
                username,
                password,
                allowed_minutes,
                role,
                is_active,
                is_local_only
            )
            VALUES (
                $username,
                $password,
                $allowed_minutes,
                $role,
                1,
                0
            );
            """;

            using var command = connection.CreateCommand();
            command.CommandText = sql;

            command.Parameters.AddWithValue(
                "$username",
                username);

            command.Parameters.AddWithValue(
                "$password",
                password);

            command.Parameters.AddWithValue(
                "$allowed_minutes",
                allowedMinutes);

            command.Parameters.AddWithValue(
                "$role",
                role);

            command.ExecuteNonQuery();
            return true;
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    public bool UpdateUser(
     int id,
     string username,
     string password,
     int allowedMinutes,
     string role)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            const string sql = """
            UPDATE users
            SET username = $username,
                password = $password,
                allowed_minutes = $allowed_minutes,
                role = $role
            WHERE id = $id;
            """;

            using var command = connection.CreateCommand();
            command.CommandText = sql;

            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$username", username);
            command.Parameters.AddWithValue("$password", password);
            command.Parameters.AddWithValue(
                "$allowed_minutes",
                allowedMinutes);
            command.Parameters.AddWithValue("$role", role);

            int rows = command.ExecuteNonQuery();
            return rows > 0;
        }
        catch (SqliteException)
        {
            return false;
        }
    }
    public bool DeleteUser(int id)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            const string sql = """
            DELETE FROM users
            WHERE id = $id
              AND NOT (
                  username = 'admin'
                  AND is_local_only = 1
              );
            """;

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$id", id);

            int rows = command.ExecuteNonQuery();
            return rows > 0;
        }
        catch (SqliteException)
        {
            return false;
        }
    }
}

public class UserRecord
{
    public int Id { get; set; }

    public int? ExternalUserId { get; set; }

    public string Username { get; set; } = "";

    public string Password { get; set; } = "";

    public int AllowedMinutes { get; set; }

    public string Role { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public bool IsLocalOnly { get; set; }

    public bool IsConsumed { get; set; }

    public bool DeactivationPending { get; set; }
}

public sealed class PendingUserDeactivation
{
    public int LocalUserId { get; init; }

    public int ExternalUserId { get; init; }
}
