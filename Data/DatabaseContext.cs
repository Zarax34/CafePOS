using System.IO;
using Microsoft.Data.Sqlite;

namespace CafePOS.Data;

public static class DatabaseContext
{
    private static readonly string _dbPath;
    private static readonly string _connectionString;

    static DatabaseContext()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dataDir = Path.Combine(baseDir, "Data");
        Directory.CreateDirectory(dataDir);
        _dbPath = Path.Combine(dataDir, "cafepos.db");
        _connectionString = $"Data Source={_dbPath}";
    }

    public static SqliteConnection GetConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    public static void Initialize()
    {
        using var connection = GetConnection();
        connection.Open();

        // Enable WAL mode for better concurrent read performance
        using (var walCmd = connection.CreateCommand())
        {
            walCmd.CommandText = "PRAGMA journal_mode=WAL;";
            walCmd.ExecuteNonQuery();
        }

        // Enable foreign keys
        using (var fkCmd = connection.CreateCommand())
        {
            fkCmd.CommandText = "PRAGMA foreign_keys=ON;";
            fkCmd.ExecuteNonQuery();
        }

        CreateTables(connection);
        MigrateDatabase(connection);
        SeedData(connection);
    }

    private static void MigrateDatabase(SqliteConnection connection)
    {
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE users ADD COLUMN FullName TEXT NOT NULL DEFAULT '';";
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // Column already exists or table doesn't exist yet
        }
    }

    private static void CreateTables(SqliteConnection connection)
    {
        var sql = @"
            CREATE TABLE IF NOT EXISTS users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Username TEXT NOT NULL UNIQUE,
                PasswordHash TEXT NOT NULL,
                Role TEXT NOT NULL DEFAULT 'cashier',
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedAt TEXT NOT NULL,
                FullName TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS categories (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                IsActive INTEGER NOT NULL DEFAULT 1
            );

            CREATE TABLE IF NOT EXISTS products (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Price REAL NOT NULL,
                CategoryId INTEGER NOT NULL,
                ImagePath TEXT,
                IsActive INTEGER NOT NULL DEFAULT 1,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY(CategoryId) REFERENCES categories(Id)
            );

            CREATE TABLE IF NOT EXISTS orders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                InvoiceNumber TEXT NOT NULL UNIQUE,
                OrderNumber INTEGER NOT NULL,
                CustomerName TEXT,
                Subtotal REAL NOT NULL,
                DiscountPercent REAL NOT NULL DEFAULT 0,
                DiscountAmount REAL NOT NULL DEFAULT 0,
                Total REAL NOT NULL,
                CashierId INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY(CashierId) REFERENCES users(Id)
            );

            CREATE TABLE IF NOT EXISTS order_items (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                OrderId INTEGER NOT NULL,
                ProductId INTEGER NOT NULL,
                ProductName TEXT NOT NULL,
                Price REAL NOT NULL,
                Quantity INTEGER NOT NULL DEFAULT 1,
                Subtotal REAL NOT NULL,
                FOREIGN KEY(OrderId) REFERENCES orders(Id)
            );

            CREATE TABLE IF NOT EXISTS returns (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                OrderId INTEGER NOT NULL,
                InvoiceNumber TEXT NOT NULL,
                Reason TEXT NOT NULL,
                TotalRefund REAL NOT NULL,
                CashierId INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY(OrderId) REFERENCES orders(Id)
            );

            CREATE TABLE IF NOT EXISTS return_items (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ReturnId INTEGER NOT NULL,
                OrderItemId INTEGER NOT NULL,
                ProductName TEXT NOT NULL,
                Price REAL NOT NULL,
                Quantity INTEGER NOT NULL,
                Subtotal REAL NOT NULL,
                FOREIGN KEY(ReturnId) REFERENCES returns(Id)
            );

            CREATE TABLE IF NOT EXISTS shifts (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CashierId INTEGER NOT NULL,
                StartTime TEXT NOT NULL,
                EndTime TEXT,
                ExpectedCash REAL NOT NULL DEFAULT 0,
                ActualCash REAL NOT NULL DEFAULT 0,
                Difference REAL NOT NULL DEFAULT 0,
                Status TEXT NOT NULL DEFAULT 'open',
                FOREIGN KEY(CashierId) REFERENCES users(Id)
            );

            CREATE TABLE IF NOT EXISTS settings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS daily_counters (
                Date TEXT PRIMARY KEY,
                LastInvoiceSeq INTEGER NOT NULL DEFAULT 0,
                LastOrderNum INTEGER NOT NULL DEFAULT 0
            );
        ";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void SeedData(SqliteConnection connection)
    {
        // Seed admin user if no users exist
        using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM users;";
            var userCount = Convert.ToInt64(checkCmd.ExecuteScalar());

            if (userCount == 0)
            {
                var passwordHash = BCrypt.Net.BCrypt.HashPassword("admin");

                using var insertCmd = connection.CreateCommand();
                insertCmd.CommandText = @"
                    INSERT INTO users (Username, PasswordHash, Role, IsActive, CreatedAt, FullName)
                    VALUES (@username, @passwordHash, @role, 1, @createdAt, @fullName);
                ";
                insertCmd.Parameters.AddWithValue("@username", "admin");
                insertCmd.Parameters.AddWithValue("@passwordHash", passwordHash);
                insertCmd.Parameters.AddWithValue("@role", "manager");
                insertCmd.Parameters.AddWithValue("@createdAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                insertCmd.Parameters.AddWithValue("@fullName", "المدير العام");
                insertCmd.ExecuteNonQuery();
            }
        }

        // Seed default settings if settings table is empty
        using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM settings;";
            var settingsCount = Convert.ToInt64(checkCmd.ExecuteScalar());

            if (settingsCount == 0)
            {
                var defaultSettings = new Dictionary<string, string>
                {
                    { "cafe_name", "كافيه" },
                    { "phone", "" },
                    { "footer", "شكراً لزيارتكم" },
                    { "logo_path", "" },
                    { "discount_enabled", "0" },
                    { "discount_percent", "10" },
                    { "printer_name", "" }
                };

                foreach (var setting in defaultSettings)
                {
                    using var insertCmd = connection.CreateCommand();
                    insertCmd.CommandText = @"
                        INSERT INTO settings (Key, Value)
                        VALUES (@key, @value);
                    ";
                    insertCmd.Parameters.AddWithValue("@key", setting.Key);
                    insertCmd.Parameters.AddWithValue("@value", setting.Value);
                    insertCmd.ExecuteNonQuery();
                }
            }
        }
    }
}
