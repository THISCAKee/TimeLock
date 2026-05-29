using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;

namespace TimeLockApp.Data;

public class DatabaseService
{

    private readonly string _connectionString;

    public DatabaseService()
    {
        string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "timelock.db");
        _connectionString = $"Data Source={dbPath}";
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

        SeedDefaultUsers(connection);
    }

    private void SeedDefaultUsers(SqliteConnection connection)
    {
        string countSql = "SELECT COUNT(*) FROM users;";

        using var countCommand = connection.CreateCommand();
        countCommand.CommandText = countSql;

        long userCount = (long)countCommand.ExecuteScalar()!;

        if (userCount > 0)
        {
            return;
        }

        string insertSql = @"
            INSERT INTO users (username, password, allowed_minutes, role)
            VALUES
                ('user', '1234', 1, 'user'),
                ('admin', 'admin123', 0, 'admin');
        ";

        using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = insertSql;
        insertCommand.ExecuteNonQuery();
    }

    public UserRecord? GetUserByUsernameAndPassword(string username, string password)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        string sql = @"
            SELECT id, username, password, allowed_minutes, role
            FROM users
            WHERE username = $username AND password = $password
            LIMIT 1;
        ";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$username", username);
        command.Parameters.AddWithValue("$password", password);

        using var reader = command.ExecuteReader();

        if (!reader.Read())
        {
            return null;
        }

        return new UserRecord
        {
            Id = reader.GetInt32(0),
            Username = reader.GetString(1),
            Password = reader.GetString(2),
            AllowedMinutes = reader.GetInt32(3),
            Role = reader.GetString(4)
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

        string sql = @"
            SELECT id, username, password, allowed_minutes, role
            FROM users
            ORDER BY id;
        ";

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            users.Add(new UserRecord
            {
                Id = reader.GetInt32(0),
                Username = reader.GetString(1),
                Password = reader.GetString(2),
                AllowedMinutes = reader.GetInt32(3),
                Role = reader.GetString(4)
            });
        }

        return users;
    }

    public bool AddUser(string username, string password, int allowedMinutes, string role)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            string sql = @"
                INSERT INTO users (username, password, allowed_minutes, role)
                VALUES ($username, $password, $allowed_minutes, $role);
            ";

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$username", username);
            command.Parameters.AddWithValue("$password", password);
            command.Parameters.AddWithValue("$allowed_minutes", allowedMinutes);
            command.Parameters.AddWithValue("$role", role);

            command.ExecuteNonQuery();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool UpdateUser(int id, string username, string password, int allowedMinutes, string role)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            string sql = @"
                UPDATE users
                SET username = $username,
                    password = $password,
                    allowed_minutes = $allowed_minutes,
                    role = $role
                WHERE id = $id;
            ";

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$username", username);
            command.Parameters.AddWithValue("$password", password);
            command.Parameters.AddWithValue("$allowed_minutes", allowedMinutes);
            command.Parameters.AddWithValue("$role", role);

            int rows = command.ExecuteNonQuery();
            return rows > 0;
        }
        catch
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

            string sql = "DELETE FROM users WHERE id = $id;";

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$id", id);

            int rows = command.ExecuteNonQuery();
            return rows > 0;
        }
        catch
        {
            return false;
        }
    }

}

public class UserRecord
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public int AllowedMinutes { get; set; }
    public string Role { get; set; } = "";
}