using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using TimeLockApp.Models;
using TimeLockApp.Services;

namespace TimeLockApp.Data;

public class DatabaseService
{

    private readonly string _connectionString;


    public DatabaseService()
    {
        string dataDirectory = Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.CommonApplicationData),
        "TimeLockApp");

        Directory.CreateDirectory(dataDirectory);

        string dbPath = Path.Combine(
            dataDirectory,
            "timelock.db");

        _connectionString = $"Data Source={dbPath}";
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

    public bool SynchronizeUsers(
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
            int affectedRows = 0;

            foreach (GoogleSheetUser user in sheetUsers)
            {
                affectedRows += UpsertSheetUser(
                    connection,
                    transaction,
                    user);
            }

            affectedRows += DeleteMissingSheetUsers(
                connection,
                transaction,
                sheetUsers);

            transaction.Commit();
            return affectedRows > 0;
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

    public int StartSession(string username, int allowedMinutes)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        const string sql = """
        INSERT INTO sessions (user_id, username, start_time, allowed_minutes, status)
        VALUES (NULL, $username, $start_time, $allowed_minutes, 'active');
        SELECT last_insert_rowid();
        """;
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$username", username);
        command.Parameters.AddWithValue("$start_time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        command.Parameters.AddWithValue("$allowed_minutes", allowedMinutes);
        return (int)(long)command.ExecuteScalar()!;
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
                        LanguageService.Default.Get("SessionNotFound"));
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

                userCommand.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    internal int RecoverInterruptedSessions(DateTime recoveryTime)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using SqliteTransaction transaction =
            connection.BeginTransaction();

        try
        {
            var activeSessions =
                new List<InterruptedSession>();

            const string selectSql = """
            SELECT id,
                   user_id,
                   start_time,
                   allowed_minutes
            FROM sessions
            WHERE status = 'active';
            """;

            using (SqliteCommand selectCommand =
                   connection.CreateCommand())
            {
                selectCommand.Transaction = transaction;
                selectCommand.CommandText = selectSql;

                using SqliteDataReader reader =
                    selectCommand.ExecuteReader();

                while (reader.Read())
                {
                    activeSessions.Add(new InterruptedSession
                    {
                        SessionId = reader.GetInt32(0),
                        UserId = reader.IsDBNull(1)
                            ? null
                            : reader.GetInt32(1),
                        StartTime = DateTime.ParseExact(
                            reader.GetString(2),
                            "yyyy-MM-dd HH:mm:ss",
                            CultureInfo.InvariantCulture),
                        AllowedMinutes = reader.GetInt32(3)
                    });
                }
            }

            string recoveryTimeText = recoveryTime.ToString(
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture);

            foreach (InterruptedSession activeSession in activeSessions)
            {
                long elapsedSeconds = (long)Math.Floor(
                    (recoveryTime - activeSession.StartTime)
                    .TotalSeconds);
                long maximumSeconds =
                    (long)activeSession.AllowedMinutes * 60;
                int usedSeconds = (int)Math.Clamp(
                    elapsedSeconds,
                    0L,
                    maximumSeconds);

                const string updateSessionSql = """
                UPDATE sessions
                SET end_time = $end_time,
                    used_seconds = $used_seconds,
                    status = 'forced_logout'
                WHERE id = $session_id
                  AND status = 'active';
                """;

                using (SqliteCommand sessionCommand =
                       connection.CreateCommand())
                {
                    sessionCommand.Transaction = transaction;
                    sessionCommand.CommandText = updateSessionSql;
                    sessionCommand.Parameters.AddWithValue(
                        "$end_time",
                        recoveryTimeText);
                    sessionCommand.Parameters.AddWithValue(
                        "$used_seconds",
                        usedSeconds);
                    sessionCommand.Parameters.AddWithValue(
                        "$session_id",
                        activeSession.SessionId);

                    if (sessionCommand.ExecuteNonQuery() != 1)
                    {
                        throw new InvalidOperationException(
                            "Interrupted session changed during recovery.");
                    }
                }

                if (!activeSession.UserId.HasValue)
                {
                    continue;
                }

                const string updateUserSql = """
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

                using SqliteCommand userCommand =
                    connection.CreateCommand();
                userCommand.Transaction = transaction;
                userCommand.CommandText = updateUserSql;
                userCommand.Parameters.AddWithValue(
                    "$user_id",
                    activeSession.UserId.Value);
                userCommand.ExecuteNonQuery();
            }

            transaction.Commit();
            return activeSessions.Count;
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
                UserId = reader.IsDBNull(1)
                    ? null
                    : reader.GetInt32(1),
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

        string createSessionsTableSql = @"
        CREATE TABLE IF NOT EXISTS sessions (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            user_id INTEGER,
            username TEXT NOT NULL,
            start_time TEXT NOT NULL,
            end_time TEXT,
            allowed_minutes INTEGER NOT NULL,
            used_seconds INTEGER DEFAULT 0,
            status TEXT NOT NULL DEFAULT 'active',
            FOREIGN KEY (user_id) REFERENCES users(id)
                ON DELETE SET NULL
        );
    ";

        using var createSessionsCommand = connection.CreateCommand();
        createSessionsCommand.CommandText = createSessionsTableSql;
        createSessionsCommand.ExecuteNonQuery();

        EnsureSessionsSchemaSupportsUserDeletion(connection);

        // Version 2 authenticates all normal users through the gateway and stores
        // the local admin verifier in a DPAPI-protected configuration file.
        using var removeLegacyCredentials = connection.CreateCommand();
        removeLegacyCredentials.CommandText = "DELETE FROM users;";
        removeLegacyCredentials.ExecuteNonQuery();

    }

    private static void EnsureSessionsSchemaSupportsUserDeletion(
        SqliteConnection connection)
    {
        if (SessionUserIdIsNullable(connection) &&
            SessionForeignKeySetsNullOnDelete(connection))
        {
            return;
        }

        using SqliteTransaction transaction =
            connection.BeginTransaction();

        try
        {
            const string sql = """
            DROP TABLE IF EXISTS sessions_migrated;

            CREATE TABLE sessions_migrated (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id INTEGER,
                username TEXT NOT NULL,
                start_time TEXT NOT NULL,
                end_time TEXT,
                allowed_minutes INTEGER NOT NULL,
                used_seconds INTEGER DEFAULT 0,
                status TEXT NOT NULL DEFAULT 'active',
                FOREIGN KEY (user_id) REFERENCES users(id)
                    ON DELETE SET NULL
            );

            INSERT INTO sessions_migrated (
                id,
                user_id,
                username,
                start_time,
                end_time,
                allowed_minutes,
                used_seconds,
                status
            )
            SELECT id,
                   user_id,
                   username,
                   start_time,
                   end_time,
                   allowed_minutes,
                   used_seconds,
                   status
            FROM sessions;

            DROP TABLE sessions;
            ALTER TABLE sessions_migrated RENAME TO sessions;
            """;

            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.ExecuteNonQuery();

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static bool SessionUserIdIsNullable(
        SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(sessions);";

        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            if (string.Equals(
                    reader.GetString(1),
                    "user_id",
                    StringComparison.OrdinalIgnoreCase))
            {
                return reader.GetInt32(3) == 0;
            }
        }

        throw new InvalidOperationException(
            "The sessions table does not contain user_id.");
    }

    private static bool SessionForeignKeySetsNullOnDelete(
        SqliteConnection connection)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_key_list(sessions);";

        using SqliteDataReader reader = command.ExecuteReader();

        while (reader.Read())
        {
            bool isUserForeignKey =
                string.Equals(
                    reader.GetString(2),
                    "users",
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    reader.GetString(3),
                    "user_id",
                    StringComparison.OrdinalIgnoreCase);

            if (isUserForeignKey)
            {
                return string.Equals(
                    reader.GetString(6),
                    "SET NULL",
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
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
                LanguageService.Default.Get("DuplicateSheetUsername", duplicateUsername));
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
                LanguageService.Default.Get("AdminInSheet"));
        }
    }
    //method Insert/Update user จาก Sheet
    private static int UpsertSheetUser(
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
        WHERE users.is_local_only = 0
          AND (
              users.external_user_id IS NOT excluded.external_user_id OR
              users.password IS NOT excluded.password OR
              users.allowed_minutes IS NOT excluded.allowed_minutes OR
              users.role IS NOT excluded.role OR
              users.is_active IS NOT CASE
                  WHEN users.is_consumed = 1 THEN 0
                  ELSE excluded.is_active
              END
          );
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

        return command.ExecuteNonQuery();
    }
    //method ลบ user ที่หายจาก Sheet
    private static int DeleteMissingSheetUsers(
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
            WHERE is_local_only = 0;
            """;

            return command.ExecuteNonQuery();
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
          AND external_user_id NOT IN ({parameterList});
        """;

        return command.ExecuteNonQuery();
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
        public int? UserId { get; set; }
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

internal sealed class InterruptedSession
{
    public int SessionId { get; init; }

    public int? UserId { get; init; }

    public DateTime StartTime { get; init; }

    public int AllowedMinutes { get; init; }
}
