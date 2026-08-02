using Microsoft.Data.Sqlite;
using CafePOS.Helpers;

namespace CafePOS.Data;

public static class DatabaseContext
{
    private static readonly string _dbPath;
    private static readonly string _connectionString;

    static DatabaseContext()
    {
        _dbPath = AppPaths.DatabasePath;
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
        AddColumnIfMissing(connection, "users", "FullName", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "daily_counters", "LastPurchaseSeq", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, "products", "IsPurchaseOnly", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, "purchases", "AttachmentPath", "TEXT");
        AddColumnIfMissing(connection, "purchases", "AttachmentFileName", "TEXT");
        AddColumnIfMissing(connection, "purchases", "ExternalInvoiceNumber", "TEXT");
        AddColumnIfMissing(connection, "expenses", "InvoiceNumber", "TEXT");
        AddColumnIfMissing(connection, "daily_counters", "LastExpenseSeq", "INTEGER NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, "expenses", "ExpenseType", "TEXT NOT NULL DEFAULT 'سداد'");
        AddColumnIfMissing(connection, "expenses", "WorkerId", "INTEGER");
        AddColumnIfMissing(connection, "orders", "PaymentMethod", "TEXT");
        AddColumnIfMissing(connection, "shifts", "DepositAmount", "REAL NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, "shifts", "PettyCashAmount", "REAL NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, "users", "PermissionsJson", "TEXT NOT NULL DEFAULT '{}'");

        // Retroactively mark products that exist in purchase_items but NOT in order_items as IsPurchaseOnly = 1
        try
        {
            using var retro = connection.CreateCommand();
            retro.CommandText = @"
                UPDATE products SET IsPurchaseOnly = 1
                WHERE Id IN (
                    SELECT DISTINCT pi.ProductId FROM purchase_items pi
                    WHERE pi.ProductId NOT IN (SELECT DISTINCT oi.ProductId FROM order_items oi)
                );
            ";
            retro.ExecuteNonQuery();
        }
        catch { }
    }

    private static void AddColumnIfMissing(SqliteConnection connection, string table, string column, string definition)
    {
        using var check = connection.CreateCommand();
        check.CommandText = $"PRAGMA table_info({table});";
        using var reader = check.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1) == column)
                return;
        }

        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        alter.ExecuteNonQuery();
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
                IsPurchaseOnly INTEGER NOT NULL DEFAULT 0,
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
                DepositAmount REAL NOT NULL DEFAULT 0,
                PettyCashAmount REAL NOT NULL DEFAULT 0,
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
                LastOrderNum INTEGER NOT NULL DEFAULT 0,
                LastPurchaseSeq INTEGER NOT NULL DEFAULT 0,
                LastExpenseSeq INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS payment_methods (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS purchases (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                InvoiceNumber TEXT NOT NULL UNIQUE,
                SupplierName TEXT,
                Notes TEXT,
                Total REAL NOT NULL,
                CreatedBy INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY(CreatedBy) REFERENCES users(Id)
            );

            CREATE TABLE IF NOT EXISTS purchase_items (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                PurchaseId INTEGER NOT NULL,
                ProductId INTEGER NOT NULL,
                ProductName TEXT NOT NULL,
                CostPrice REAL NOT NULL,
                Quantity INTEGER NOT NULL DEFAULT 1,
                Subtotal REAL NOT NULL,
                FOREIGN KEY(PurchaseId) REFERENCES purchases(Id)
            );

            CREATE TABLE IF NOT EXISTS workers (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Phone TEXT,
                IsActive INTEGER NOT NULL DEFAULT 1,
                CreatedAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS permissions (
                Key TEXT PRIMARY KEY,
                NameArabic TEXT NOT NULL,
                Category TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS user_permissions (
                UserId INTEGER NOT NULL,
                PermissionKey TEXT NOT NULL,
                IsAllowed INTEGER NOT NULL DEFAULT 1,
                PRIMARY KEY (UserId, PermissionKey),
                FOREIGN KEY(UserId) REFERENCES users(Id),
                FOREIGN KEY(PermissionKey) REFERENCES permissions(Key)
            );

            CREATE TABLE IF NOT EXISTS expenses (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                InvoiceNumber TEXT,
                Description TEXT NOT NULL,
                Amount REAL NOT NULL,
                CashierId INTEGER NOT NULL,
                ShiftId INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                ExpenseType TEXT NOT NULL DEFAULT 'سداد',
                WorkerId INTEGER,
                FOREIGN KEY(CashierId) REFERENCES users(Id),
                FOREIGN KEY(ShiftId) REFERENCES shifts(Id)
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

        // Seed default permissions if permissions table is empty
        using (var checkPermCmd = connection.CreateCommand())
        {
            checkPermCmd.CommandText = "SELECT COUNT(*) FROM permissions;";
            var permCount = Convert.ToInt64(checkPermCmd.ExecuteScalar());

            if (permCount == 0)
            {
                var defaultPermissions = new[]
                {
                    // نقطة البيع
                    ("pos_access", "الوصول إلى نقطة البيع", "نقطة البيع"),
                    ("pos_checkout", "إتمام عملية البيع", "نقطة البيع"),
                    ("pos_discount", "تطبيق الخصم", "نقطة البيع"),
                    ("pos_cancel", "إلغاء السلة", "نقطة البيع"),
                    // الفواتير
                    ("invoices_view", "عرض الفواتير", "الفواتير"),
                    ("invoices_search", "البحث في الفواتير", "الفواتير"),
                    ("invoices_print", "طباعة الفواتير", "الفواتير"),
                    // المنتجات
                    ("products_view", "عرض المنتجات", "المنتجات"),
                    ("products_add", "إضافة منتج", "المنتجات"),
                    ("products_edit", "تعديل منتج", "المنتجات"),
                    ("products_delete", "حذف منتج", "المنتجات"),
                    ("categories_manage", "إدارة الفئات", "المنتجات"),
                    // المشتريات
                    ("purchases_view", "عرض المشتريات", "المشتريات"),
                    ("purchases_add", "إضافة مشتريات", "المشتريات"),
                    ("purchases_edit", "تعديل مشتريات", "المشتريات"),
                    ("purchases_delete", "حذف مشتريات", "المشتريات"),
                    // المصروفات
                    ("expenses_view", "عرض المصروفات", "المصروفات"),
                    ("expenses_add", "إضافة مصروف", "المصروفات"),
                    ("expenses_edit", "تعديل مصروف", "المصروفات"),
                    ("expenses_delete", "حذف مصروف", "المصروفات"),
                    // العمال
                    ("workers_view", "عرض العمال", "العمال"),
                    ("workers_add", "إضافة عامل", "العمال"),
                    ("workers_edit", "تعديل عامل", "العمال"),
                    ("workers_delete", "حذف عامل", "العمال"),
                    ("workers_withdrawals", "إدارة سحوبات العمال", "العمال"),
                    // الطلبات والمرتجعات
                    ("orders_void", "إلغاء فاتورة", "الطلبات والمرتجعات"),
                    ("returns_manage", "إدارة المرتجعات", "الطلبات والمرتجعات"),
                    // التقارير
                    ("reports_view", "عرض التقارير", "التقارير"),
                    ("reports_export_excel", "تصدير Excel", "التقارير"),
                    ("reports_export_pdf", "تصدير PDF", "التقارير"),
                    ("shift_close", "إغلاق الشفت", "التقارير"),
                    // الإعدادات
                    ("settings_access", "الوصول إلى الإعدادات", "الإعدادات"),
                    ("users_manage", "إدارة المستخدمين والصلاحيات", "الإعدادات"),
                    ("license_manage", "إدارة الترخيص", "الإعدادات"),
                    ("backup_manage", "النسخ الاحتياطي", "الإعدادات"),
                    // إدارية
                    ("full_access", "وصول كامل (مدير النظام)", "إدارية"),
                };

                foreach (var (key, nameArabic, category) in defaultPermissions)
                {
                    using var insertCmd = connection.CreateCommand();
                    insertCmd.CommandText = @"
                        INSERT OR IGNORE INTO permissions (Key, NameArabic, Category)
                        VALUES (@key, @name, @category);
                    ";
                    insertCmd.Parameters.AddWithValue("@key", key);
                    insertCmd.Parameters.AddWithValue("@name", nameArabic);
                    insertCmd.Parameters.AddWithValue("@category", category);
                    insertCmd.ExecuteNonQuery();
                }
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
                    { "printer_name", "" },
                    { "returns_enabled", "1" },
                    { "raster_print", "1" },
                    { "compact_receipt", "0" },
                    { "invert_receipt_colors", "0" }
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
