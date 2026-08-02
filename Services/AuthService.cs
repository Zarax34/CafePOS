using System.Text.Json;
using CafePOS.Data;
using CafePOS.Models;
using Microsoft.Data.Sqlite;

namespace CafePOS.Services;

public static class AuthService
{
    public static User? CurrentUser { get; private set; }

    /// <summary>
    /// Authenticates a user with username and password.
    /// </summary>
    public static User? Login(string username, string password)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, Username, PasswordHash, Role, IsActive, CreatedAt, FullName, PermissionsJson
            FROM users
            WHERE Username = @username AND IsActive = 1;
        ";
        cmd.Parameters.AddWithValue("@username", username);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return null;

        var storedHash = reader.GetString(2);

        if (!BCrypt.Net.BCrypt.Verify(password, storedHash))
            return null;

        var user = new User
        {
            Id = reader.GetInt32(0),
            Username = reader.GetString(1),
            PasswordHash = storedHash,
            Role = reader.GetString(3),
            IsActive = reader.GetInt32(4) == 1,
            CreatedAt = DateTime.Parse(reader.GetString(5)),
            FullName = reader.IsDBNull(6) ? "" : reader.GetString(6),
            PermissionsJson = reader.IsDBNull(7) ? "{}" : reader.GetString(7)
        };

        CurrentUser = user;
        return user;
    }

    /// <summary>
    /// Creates a new user with hashed password.
    /// </summary>
    public static bool CreateUser(string username, string password, string role, string fullName)
    {
        try
        {
            using var connection = DatabaseContext.GetConnection();
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO users (Username, PasswordHash, Role, IsActive, CreatedAt, FullName)
                VALUES (@username, @passwordHash, @role, 1, @createdAt, @fullName);
            ";
            cmd.Parameters.AddWithValue("@username", username);
            cmd.Parameters.AddWithValue("@passwordHash", BCrypt.Net.BCrypt.HashPassword(password));
            cmd.Parameters.AddWithValue("@role", role);
            cmd.Parameters.AddWithValue("@createdAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("@fullName", fullName);

            return cmd.ExecuteNonQuery() > 0;
        }
        catch (SqliteException)
        {
            return false; // Username already exists or other constraint violation
        }
    }

    /// <summary>
    /// Changes a user's password.
    /// </summary>
    public static bool ChangePassword(int userId, string newPassword)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE users SET PasswordHash = @hash WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@hash", BCrypt.Net.BCrypt.HashPassword(newPassword));
        cmd.Parameters.AddWithValue("@id", userId);

        return cmd.ExecuteNonQuery() > 0;
    }

    /// <summary>
    /// Deactivates a user (soft delete).
    /// </summary>
    public static bool DeleteUser(int userId)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        string username = "";
        string role = "";
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT Username, Role FROM users WHERE Id = @id;";
            cmd.Parameters.AddWithValue("@id", userId);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                username = reader.GetString(0);
                role = reader.GetString(1);
            }
        }

        if (username == "admin")
        {
            throw new InvalidOperationException("لا يمكن تعطيل أو حذف المدير العام الافتراضي (admin).");
        }

        if (role == "manager")
        {
            using var cmdCount = connection.CreateCommand();
            cmdCount.CommandText = "SELECT COUNT(*) FROM users WHERE Role = 'manager' AND IsActive = 1;";
            int managerCount = Convert.ToInt32(cmdCount.ExecuteScalar());
            if (managerCount <= 1)
            {
                throw new InvalidOperationException("لا يمكن تعطيل المدير الوحيد في النظام.");
            }
        }

        using var cmdUpdate = connection.CreateCommand();
        cmdUpdate.CommandText = "UPDATE users SET IsActive = 0 WHERE Id = @id;";
        cmdUpdate.Parameters.AddWithValue("@id", userId);
        return cmdUpdate.ExecuteNonQuery() > 0;
    }

    /// <summary>
    /// Updates user info (username, full name, role, and optionally password).
    /// </summary>
    public static bool UpdateUser(int userId, string username, string fullName, string role, string? newPassword = null)
    {
        try
        {
            using var connection = DatabaseContext.GetConnection();
            connection.Open();

            if (!string.IsNullOrEmpty(newPassword))
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    UPDATE users SET Username = @username, FullName = @fullName, Role = @role, PasswordHash = @hash
                    WHERE Id = @id;
                ";
                cmd.Parameters.AddWithValue("@hash", BCrypt.Net.BCrypt.HashPassword(newPassword));
                cmd.Parameters.AddWithValue("@username", username);
                cmd.Parameters.AddWithValue("@fullName", fullName);
                cmd.Parameters.AddWithValue("@role", role);
                cmd.Parameters.AddWithValue("@id", userId);
                return cmd.ExecuteNonQuery() > 0;
            }
            else
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    UPDATE users SET Username = @username, FullName = @fullName, Role = @role
                    WHERE Id = @id;
                ";
                cmd.Parameters.AddWithValue("@username", username);
                cmd.Parameters.AddWithValue("@fullName", fullName);
                cmd.Parameters.AddWithValue("@role", role);
                cmd.Parameters.AddWithValue("@id", userId);
                return cmd.ExecuteNonQuery() > 0;
            }
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reactivates a disabled user.
    /// </summary>
    public static bool ReactivateUser(int userId)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE users SET IsActive = 1 WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", userId);
        return cmd.ExecuteNonQuery() > 0;
    }

    /// <summary>
    /// Gets all users (for manager admin panel).
    /// </summary>
    public static List<User> GetAllUsers()
    {
        var users = new List<User>();
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Username, Role, IsActive, CreatedAt, FullName FROM users ORDER BY Id;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            users.Add(new User
            {
                Id = reader.GetInt32(0),
                Username = reader.GetString(1),
                Role = reader.GetString(2),
                IsActive = reader.GetInt32(3) == 1,
                CreatedAt = DateTime.Parse(reader.GetString(4)),
                FullName = reader.IsDBNull(5) ? "" : reader.GetString(5)
            });
        }

        return users;
    }

    /// <summary>
    /// Logs out the current user.
    /// </summary>
    public static void Logout()
    {
        CurrentUser = null;
    }

    /// <summary>
    /// Gets all defined permissions (for the permissions editor UI).
    /// </summary>
    public static List<Permission> GetAllPermissions()
    {
        var permissions = new List<Permission>();
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Key, NameArabic, Category FROM permissions ORDER BY Category, Key;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            permissions.Add(new Permission
            {
                Key = reader.GetString(0),
                NameArabic = reader.GetString(1),
                Category = reader.GetString(2)
            });
        }

        return permissions;
    }

    /// <summary>
    /// Gets the explicit permission dictionary of a user (empty if none configured).
    /// </summary>
    public static Dictionary<string, bool> GetPermissions(int userId)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT PermissionsJson FROM users WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", userId);

        var raw = cmd.ExecuteScalar() as string;
        if (string.IsNullOrWhiteSpace(raw)) return new();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, bool>>(raw) ?? new();
        }
        catch
        {
            return new();
        }
    }

    /// <summary>
    /// Saves the explicit permission dictionary of a user.
    /// </summary>
    public static bool SavePermissions(int userId, Dictionary<string, bool> permissions)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE users SET PermissionsJson = @json WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@json", JsonSerializer.Serialize(permissions));
        cmd.Parameters.AddWithValue("@id", userId);

        return cmd.ExecuteNonQuery() > 0;
    }
}
