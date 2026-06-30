using System.IO;
using System.Text.Json;
using CafePOS.Data;
using CafePOS.Models;
using Microsoft.Data.Sqlite;

namespace CafePOS.Services;

public class BackupData
{
    public List<UserBackup> Users { get; set; } = new();
    public List<CategoryBackup> Categories { get; set; } = new();
    public List<ProductBackup> Products { get; set; } = new();
    public List<OrderBackup> Orders { get; set; } = new();
    public List<ReturnBackup> Returns { get; set; } = new();
    public List<ShiftBackup> Shifts { get; set; } = new();
}

public class UserBackup
{
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CategoryBackup
{
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}

public class ProductBackup
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? ImagePath { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}

public class OrderBackup
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public int OrderNumber { get; set; }
    public string? CustomerName { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Total { get; set; }
    public string CashierUsername { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<OrderItemBackup> Items { get; set; } = new();
}

public class OrderItemBackup
{
    public string ProductName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public decimal Subtotal { get; set; }
}

public class ReturnBackup
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public decimal TotalRefund { get; set; }
    public string CashierUsername { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<ReturnItemBackup> Items { get; set; } = new();
}

public class ReturnItemBackup
{
    public string ProductName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public decimal Subtotal { get; set; }
}

public class ShiftBackup
{
    public string CashierUsername { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal ActualCash { get; set; }
    public decimal Difference { get; set; }
    public string Status { get; set; } = string.Empty;
}

public static class BackupService
{
    /// <summary>
    /// Exports all database tables to a JSON file.
    /// </summary>
    public static void ExportBackup(string filePath)
    {
        var data = new BackupData();

        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        // 1. Users
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT Username, PasswordHash, Role, IsActive, CreatedAt FROM users;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                data.Users.Add(new UserBackup
                {
                    Username = reader.GetString(0),
                    PasswordHash = reader.GetString(1),
                    Role = reader.GetString(2),
                    IsActive = reader.GetInt32(3) == 1,
                    CreatedAt = DateTime.Parse(reader.GetString(4))
                });
            }
        }

        // 2. Categories
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT Name, SortOrder, IsActive FROM categories;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                data.Categories.Add(new CategoryBackup
                {
                    Name = reader.GetString(0),
                    SortOrder = reader.GetInt32(1),
                    IsActive = reader.GetInt32(2) == 1
                });
            }
        }

        // 3. Products
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT p.Name, p.Price, c.Name, p.ImagePath, p.IsActive, p.SortOrder
                FROM products p
                JOIN categories c ON p.CategoryId = c.Id;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                data.Products.Add(new ProductBackup
                {
                    Name = reader.GetString(0),
                    Price = (decimal)reader.GetDouble(1),
                    CategoryName = reader.GetString(2),
                    ImagePath = reader.IsDBNull(3) ? null : reader.GetString(3),
                    IsActive = reader.GetInt32(4) == 1,
                    SortOrder = reader.GetInt32(5)
                });
            }
        }

        // 4. Orders
        var orderMap = new Dictionary<int, OrderBackup>();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT o.Id, o.InvoiceNumber, o.OrderNumber, o.CustomerName, o.Subtotal,
                       o.DiscountPercent, o.DiscountAmount, o.Total, u.Username, o.CreatedAt
                FROM orders o
                JOIN users u ON o.CashierId = u.Id;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetInt32(0);
                var order = new OrderBackup
                {
                    InvoiceNumber = reader.GetString(1),
                    OrderNumber = reader.GetInt32(2),
                    CustomerName = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Subtotal = (decimal)reader.GetDouble(4),
                    DiscountPercent = (decimal)reader.GetDouble(5),
                    DiscountAmount = (decimal)reader.GetDouble(6),
                    Total = (decimal)reader.GetDouble(7),
                    CashierUsername = reader.GetString(8),
                    CreatedAt = DateTime.Parse(reader.GetString(9))
                };
                orderMap[id] = order;
                data.Orders.Add(order);
            }
        }

        // Order Items
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT OrderId, ProductName, Price, Quantity, Subtotal FROM order_items;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var orderId = reader.GetInt32(0);
                if (orderMap.TryGetValue(orderId, out var order))
                {
                    order.Items.Add(new OrderItemBackup
                    {
                        ProductName = reader.GetString(1),
                        Price = (decimal)reader.GetDouble(2),
                        Quantity = reader.GetInt32(3),
                        Subtotal = (decimal)reader.GetDouble(4)
                    });
                }
            }
        }

        // 5. Returns
        var returnMap = new Dictionary<int, ReturnBackup>();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT r.Id, r.InvoiceNumber, r.Reason, r.TotalRefund, u.Username, r.CreatedAt
                FROM returns r
                JOIN users u ON r.CashierId = u.Id;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var id = reader.GetInt32(0);
                var ret = new ReturnBackup
                {
                    InvoiceNumber = reader.GetString(1),
                    Reason = reader.GetString(2),
                    TotalRefund = (decimal)reader.GetDouble(3),
                    CashierUsername = reader.GetString(4),
                    CreatedAt = DateTime.Parse(reader.GetString(5))
                };
                returnMap[id] = ret;
                data.Returns.Add(ret);
            }
        }

        // Return Items
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT ReturnId, ProductName, Price, Quantity, Subtotal FROM return_items;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var returnId = reader.GetInt32(0);
                if (returnMap.TryGetValue(returnId, out var ret))
                {
                    ret.Items.Add(new ReturnItemBackup
                    {
                        ProductName = reader.GetString(1),
                        Price = (decimal)reader.GetDouble(2),
                        Quantity = reader.GetInt32(3),
                        Subtotal = (decimal)reader.GetDouble(4)
                    });
                }
            }
        }

        // 6. Shifts
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT u.Username, s.StartTime, s.EndTime, s.ExpectedCash, s.ActualCash, s.Difference, s.Status
                FROM shifts s
                JOIN users u ON s.CashierId = u.Id;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                data.Shifts.Add(new ShiftBackup
                {
                    CashierUsername = reader.GetString(0),
                    StartTime = DateTime.Parse(reader.GetString(1)),
                    EndTime = reader.IsDBNull(2) ? null : DateTime.Parse(reader.GetString(2)),
                    ExpectedCash = (decimal)reader.GetDouble(3),
                    ActualCash = (decimal)reader.GetDouble(4),
                    Difference = (decimal)reader.GetDouble(5),
                    Status = reader.GetString(6)
                });
            }
        }

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Smart import: parses JSON backup, avoids duplicate invoices, and inserts new records.
    /// Returns a message describing what was imported.
    /// </summary>
    public static string ImportBackupAndSync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("ملف النسخة الاحتياطية غير موجود");

        var json = File.ReadAllText(filePath);
        var data = JsonSerializer.Deserialize<BackupData>(json);
        if (data == null)
            throw new InvalidDataException("صيغة ملف النسخ الاحتياطي غير صالحة");

        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();
        try
        {
            int usersAdded = 0;
            int categoriesAdded = 0;
            int productsAdded = 0;
            int ordersAdded = 0;
            int returnsAdded = 0;
            int shiftsAdded = 0;

            // 1. Sync Users
            foreach (var ub in data.Users)
            {
                bool exists = false;
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "SELECT COUNT(*) FROM users WHERE Username = @u;";
                    cmd.Parameters.AddWithValue("@u", ub.Username);
                    exists = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }

                if (!exists)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"
                        INSERT INTO users (Username, PasswordHash, Role, IsActive, CreatedAt)
                        VALUES (@u, @p, @r, @a, @c);";
                    cmd.Parameters.AddWithValue("@u", ub.Username);
                    cmd.Parameters.AddWithValue("@p", ub.PasswordHash);
                    cmd.Parameters.AddWithValue("@r", ub.Role);
                    cmd.Parameters.AddWithValue("@a", ub.IsActive ? 1 : 0);
                    cmd.Parameters.AddWithValue("@c", ub.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.ExecuteNonQuery();
                    usersAdded++;
                }
            }

            // Get maps of usernames to IDs for linking
            var userMap = new Dictionary<string, int>();
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = "SELECT Id, Username FROM users;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    userMap[reader.GetString(1)] = reader.GetInt32(0);
                }
            }

            // 2. Sync Categories
            foreach (var cb in data.Categories)
            {
                bool exists = false;
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "SELECT COUNT(*) FROM categories WHERE Name = @n;";
                    cmd.Parameters.AddWithValue("@n", cb.Name);
                    exists = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }

                if (!exists)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = "INSERT INTO categories (Name, SortOrder, IsActive) VALUES (@n, @s, @a);";
                    cmd.Parameters.AddWithValue("@n", cb.Name);
                    cmd.Parameters.AddWithValue("@s", cb.SortOrder);
                    cmd.Parameters.AddWithValue("@a", cb.IsActive ? 1 : 0);
                    cmd.ExecuteNonQuery();
                    categoriesAdded++;
                }
            }

            // Get categories maps
            var catMap = new Dictionary<string, int>();
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = "SELECT Id, Name FROM categories;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    catMap[reader.GetString(1)] = reader.GetInt32(0);
                }
            }

            // 3. Sync Products
            foreach (var pb in data.Products)
            {
                bool exists = false;
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "SELECT COUNT(*) FROM products WHERE Name = @n;";
                    cmd.Parameters.AddWithValue("@n", pb.Name);
                    exists = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }

                if (!exists)
                {
                    // Find category ID
                    if (!catMap.TryGetValue(pb.CategoryName, out var catId))
                    {
                        // Add category dynamically
                        using var insertCat = connection.CreateCommand();
                        insertCat.Transaction = transaction;
                        insertCat.CommandText = "INSERT INTO categories (Name, SortOrder, IsActive) VALUES (@n, 0, 1); SELECT last_insert_rowid();";
                        insertCat.Parameters.AddWithValue("@n", pb.CategoryName);
                        catId = Convert.ToInt32(insertCat.ExecuteScalar());
                        catMap[pb.CategoryName] = catId;
                        categoriesAdded++;
                    }

                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"
                        INSERT INTO products (Name, Price, CategoryId, ImagePath, IsActive, SortOrder)
                        VALUES (@n, @p, @c, @i, @a, @s);";
                    cmd.Parameters.AddWithValue("@n", pb.Name);
                    cmd.Parameters.AddWithValue("@p", (double)pb.Price);
                    cmd.Parameters.AddWithValue("@c", catId);
                    cmd.Parameters.AddWithValue("@i", (object?)pb.ImagePath ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@a", pb.IsActive ? 1 : 0);
                    cmd.Parameters.AddWithValue("@s", pb.SortOrder);
                    cmd.ExecuteNonQuery();
                    productsAdded++;
                }
            }

            // Get product map
            var prodMap = new Dictionary<string, int>();
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = "SELECT Id, Name FROM products;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    prodMap[reader.GetString(1)] = reader.GetInt32(0);
                }
            }

            // 4. Sync Orders (avoiding duplicate InvoiceNumbers)
            foreach (var ob in data.Orders)
            {
                bool exists = false;
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "SELECT COUNT(*) FROM orders WHERE InvoiceNumber = @i;";
                    cmd.Parameters.AddWithValue("@i", ob.InvoiceNumber);
                    exists = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }

                if (!exists)
                {
                    // Find cashier
                    if (!userMap.TryGetValue(ob.CashierUsername, out var cashierId))
                    {
                        cashierId = userMap.Values.FirstOrDefault(); // default to first user if not found
                    }

                    int newOrderId;
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.Transaction = transaction;
                        cmd.CommandText = @"
                            INSERT INTO orders (InvoiceNumber, OrderNumber, CustomerName, Subtotal,
                                                DiscountPercent, DiscountAmount, Total, CashierId, CreatedAt)
                            VALUES (@inv, @ord, @cust, @sub, @dp, @da, @tot, @cashier, @created);
                            SELECT last_insert_rowid();";
                        cmd.Parameters.AddWithValue("@inv", ob.InvoiceNumber);
                        cmd.Parameters.AddWithValue("@ord", ob.OrderNumber);
                        cmd.Parameters.AddWithValue("@cust", (object?)ob.CustomerName ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@sub", (double)ob.Subtotal);
                        cmd.Parameters.AddWithValue("@dp", (double)ob.DiscountPercent);
                        cmd.Parameters.AddWithValue("@da", (double)ob.DiscountAmount);
                        cmd.Parameters.AddWithValue("@tot", (double)ob.Total);
                        cmd.Parameters.AddWithValue("@cashier", cashierId);
                        cmd.Parameters.AddWithValue("@created", ob.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                        newOrderId = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    // Sync items
                    foreach (var item in ob.Items)
                    {
                        if (!prodMap.TryGetValue(item.ProductName, out var prodId))
                        {
                            prodId = 0; // fallback product ID
                        }

                        using var cmdItem = connection.CreateCommand();
                        cmdItem.Transaction = transaction;
                        cmdItem.CommandText = @"
                            INSERT INTO order_items (OrderId, ProductId, ProductName, Price, Quantity, Subtotal)
                            VALUES (@o, @p, @pn, @pr, @q, @s);";
                        cmdItem.Parameters.AddWithValue("@o", newOrderId);
                        cmdItem.Parameters.AddWithValue("@p", prodId);
                        cmdItem.Parameters.AddWithValue("@pn", item.ProductName);
                        cmdItem.Parameters.AddWithValue("@pr", (double)item.Price);
                        cmdItem.Parameters.AddWithValue("@q", item.Quantity);
                        cmdItem.Parameters.AddWithValue("@s", (double)item.Subtotal);
                        cmdItem.ExecuteNonQuery();
                    }

                    ordersAdded++;
                }
            }

            // Get synced order map
            var orderIdMap = new Dictionary<string, int>();
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = "SELECT Id, InvoiceNumber FROM orders;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    orderIdMap[reader.GetString(1)] = reader.GetInt32(0);
                }
            }

            // 5. Sync Returns
            foreach (var rb in data.Returns)
            {
                bool exists = false;
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "SELECT COUNT(*) FROM returns WHERE InvoiceNumber = @i AND CreatedAt = @c;";
                    cmd.Parameters.AddWithValue("@i", rb.InvoiceNumber);
                    cmd.Parameters.AddWithValue("@c", rb.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                    exists = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }

                if (!exists)
                {
                    // Find cashier
                    if (!userMap.TryGetValue(rb.CashierUsername, out var cashierId))
                    {
                        cashierId = userMap.Values.FirstOrDefault();
                    }

                    // Find corresponding order ID
                    if (orderIdMap.TryGetValue(rb.InvoiceNumber, out var orderId))
                    {
                        int newReturnId;
                        using (var cmd = connection.CreateCommand())
                        {
                            cmd.Transaction = transaction;
                            cmd.CommandText = @"
                                INSERT INTO returns (OrderId, InvoiceNumber, Reason, TotalRefund, CashierId, CreatedAt)
                                VALUES (@o, @inv, @r, @tot, @cashier, @created);
                                SELECT last_insert_rowid();";
                            cmd.Parameters.AddWithValue("@o", orderId);
                            cmd.Parameters.AddWithValue("@inv", rb.InvoiceNumber);
                            cmd.Parameters.AddWithValue("@r", rb.Reason);
                            cmd.Parameters.AddWithValue("@tot", (double)rb.TotalRefund);
                            cmd.Parameters.AddWithValue("@cashier", cashierId);
                            cmd.Parameters.AddWithValue("@created", rb.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                            newReturnId = Convert.ToInt32(cmd.ExecuteScalar());
                        }

                        // Sync return items
                        foreach (var rItem in rb.Items)
                        {
                            using var cmdItem = connection.CreateCommand();
                            cmdItem.Transaction = transaction;
                            cmdItem.CommandText = @"
                                INSERT INTO return_items (ReturnId, OrderItemId, ProductName, Price, Quantity, Subtotal)
                                VALUES (@r, 0, @pn, @pr, @q, @s);"; // Link order item as 0
                            cmdItem.Parameters.AddWithValue("@r", newReturnId);
                            cmdItem.Parameters.AddWithValue("@pn", rItem.ProductName);
                            cmdItem.Parameters.AddWithValue("@pr", (double)rItem.Price);
                            cmdItem.Parameters.AddWithValue("@q", rItem.Quantity);
                            cmdItem.Parameters.AddWithValue("@s", (double)rItem.Subtotal);
                            cmdItem.ExecuteNonQuery();
                        }

                        returnsAdded++;
                    }
                }
            }

            // 6. Sync Shifts
            foreach (var sb in data.Shifts)
            {
                bool exists = false;
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "SELECT COUNT(*) FROM shifts WHERE StartTime = @s;";
                    cmd.Parameters.AddWithValue("@s", sb.StartTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    exists = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
                }

                if (!exists)
                {
                    if (!userMap.TryGetValue(sb.CashierUsername, out var cashierId))
                    {
                        cashierId = userMap.Values.FirstOrDefault();
                    }

                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"
                        INSERT INTO shifts (CashierId, StartTime, EndTime, ExpectedCash, ActualCash, Difference, Status)
                        VALUES (@c, @s, @e, @ex, @ac, @d, @st);";
                    cmd.Parameters.AddWithValue("@c", cashierId);
                    cmd.Parameters.AddWithValue("@s", sb.StartTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@e", (object?)sb.EndTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ex", (double)sb.ExpectedCash);
                    cmd.Parameters.AddWithValue("@ac", (double)sb.ActualCash);
                    cmd.Parameters.AddWithValue("@d", (double)sb.Difference);
                    cmd.Parameters.AddWithValue("@st", sb.Status);
                    cmd.ExecuteNonQuery();
                    shiftsAdded++;
                }
            }

            transaction.Commit();
            return $"تمت المزامنة بنجاح:\n" +
                   $"• الفواتير المستوردة: {ordersAdded}\n" +
                   $"• المرتجعات المستوردة: {returnsAdded}\n" +
                   $"• المنتجات الجديدة: {productsAdded}\n" +
                   $"• التصنيفات الجديدة: {categoriesAdded}\n" +
                   $"• المستخدمون المضافون: {usersAdded}\n" +
                   $"• ورديات العمل (الشفتات): {shiftsAdded}";
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
