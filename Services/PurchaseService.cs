using Microsoft.Data.Sqlite;
using CafePOS.Data;
using CafePOS.Models;

namespace CafePOS.Services;

public static class PurchaseService
{
    public static Purchase CreatePurchase(Purchase purchase)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();

        try
        {
            // Generate invoice number
            var invNumber = GenerateInvoiceNumber(connection);
            purchase.InvoiceNumber = invNumber;

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO purchases (InvoiceNumber, ExternalInvoiceNumber, SupplierName, Notes, Total, CreatedBy, CreatedAt, AttachmentPath, AttachmentFileName)
                VALUES (@inv, @extInv, @supplier, @notes, @total, @createdBy, @createdAt, @attPath, @attName);
                SELECT last_insert_rowid();
            ";
            cmd.Parameters.AddWithValue("@inv", purchase.InvoiceNumber);
            cmd.Parameters.AddWithValue("@extInv", (object?)purchase.ExternalInvoiceNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@supplier", (object?)purchase.SupplierName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@notes", (object?)purchase.Notes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@total", purchase.Total);
            cmd.Parameters.AddWithValue("@createdBy", purchase.CreatedBy);
            cmd.Parameters.AddWithValue("@createdAt", purchase.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("@attPath", (object?)purchase.AttachmentPath ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@attName", (object?)purchase.AttachmentFileName ?? DBNull.Value);

            var purchaseId = Convert.ToInt32(cmd.ExecuteScalar());
            purchase.Id = purchaseId;

            // Insert items
            foreach (var item in purchase.Items)
            {
                using var itemCmd = connection.CreateCommand();
                itemCmd.CommandText = @"
                    INSERT INTO purchase_items (PurchaseId, ProductId, ProductName, CostPrice, Quantity, Subtotal)
                    VALUES (@purchaseId, @productId, @productName, @costPrice, @qty, @subtotal);
                ";
                itemCmd.Parameters.AddWithValue("@purchaseId", purchaseId);
                itemCmd.Parameters.AddWithValue("@productId", item.ProductId);
                itemCmd.Parameters.AddWithValue("@productName", item.ProductName);
                itemCmd.Parameters.AddWithValue("@costPrice", item.CostPrice);
                itemCmd.Parameters.AddWithValue("@qty", item.Quantity);
                itemCmd.Parameters.AddWithValue("@subtotal", item.Subtotal);
                itemCmd.ExecuteNonQuery();
            }

            transaction.Commit();
            return purchase;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public static Purchase? GetPurchaseByInvoice(string invoiceNumber)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        Purchase? purchase = null;

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT p.Id, p.InvoiceNumber, p.ExternalInvoiceNumber, p.SupplierName, p.Notes, p.Total, p.CreatedBy, p.CreatedAt,
                       u.FullName AS CreatorName, p.AttachmentPath, p.AttachmentFileName
                FROM purchases p
                LEFT JOIN users u ON u.Id = p.CreatedBy
                WHERE p.InvoiceNumber = @inv;
            ";
            cmd.Parameters.AddWithValue("@inv", invoiceNumber);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                purchase = new Purchase
                {
                    Id = reader.GetInt32(0),
                    InvoiceNumber = reader.GetString(1),
                    ExternalInvoiceNumber = reader.IsDBNull(2) ? null : reader.GetString(2),
                    SupplierName = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Notes = reader.IsDBNull(4) ? null : reader.GetString(4),
                    Total = reader.GetDecimal(5),
                    CreatedBy = reader.GetInt32(6),
                    CreatedAt = DateTime.Parse(reader.GetString(7)),
                    CreatorName = reader.IsDBNull(8) ? null : reader.GetString(8),
                    AttachmentPath = reader.IsDBNull(9) ? null : reader.GetString(9),
                    AttachmentFileName = reader.IsDBNull(10) ? null : reader.GetString(10)
                };
            }
        }

        if (purchase == null) return null;

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT Id, PurchaseId, ProductId, ProductName, CostPrice, Quantity, Subtotal
                FROM purchase_items WHERE PurchaseId = @purchaseId;
            ";
            cmd.Parameters.AddWithValue("@purchaseId", purchase.Id);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                purchase.Items.Add(new PurchaseItem
                {
                    Id = reader.GetInt32(0),
                    PurchaseId = reader.GetInt32(1),
                    ProductId = reader.GetInt32(2),
                    ProductName = reader.GetString(3),
                    CostPrice = reader.GetDecimal(4),
                    Quantity = reader.GetInt32(5),
                    Subtotal = reader.GetDecimal(6)
                });
            }
        }

        return purchase;
    }

    public static List<Purchase> SearchPurchases(string query)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        var purchases = new List<Purchase>();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT p.Id, p.InvoiceNumber, p.ExternalInvoiceNumber, p.SupplierName, p.Notes, p.Total, p.CreatedBy, p.CreatedAt,
                   u.FullName AS CreatorName, p.AttachmentPath, p.AttachmentFileName
            FROM purchases p
            LEFT JOIN users u ON u.Id = p.CreatedBy
            WHERE p.InvoiceNumber LIKE @query OR p.ExternalInvoiceNumber LIKE @query OR p.SupplierName LIKE @query
            ORDER BY p.Id DESC
            LIMIT 100;
        ";
        cmd.Parameters.AddWithValue("@query", $"%{query}%");

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            purchases.Add(new Purchase
            {
                Id = reader.GetInt32(0),
                InvoiceNumber = reader.GetString(1),
                ExternalInvoiceNumber = reader.IsDBNull(2) ? null : reader.GetString(2),
                SupplierName = reader.IsDBNull(3) ? null : reader.GetString(3),
                Notes = reader.IsDBNull(4) ? null : reader.GetString(4),
                Total = reader.GetDecimal(5),
                CreatedBy = reader.GetInt32(6),
                CreatedAt = DateTime.Parse(reader.GetString(7)),
                CreatorName = reader.IsDBNull(8) ? null : reader.GetString(8),
                AttachmentPath = reader.IsDBNull(9) ? null : reader.GetString(9),
                AttachmentFileName = reader.IsDBNull(10) ? null : reader.GetString(10)
            });
        }

        return purchases;
    }

    public static List<Purchase> GetPurchasesByDateRange(DateTime from, DateTime to)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        var purchases = new List<Purchase>();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT p.Id, p.InvoiceNumber, p.ExternalInvoiceNumber, p.SupplierName, p.Notes, p.Total, p.CreatedBy, p.CreatedAt,
                   u.FullName AS CreatorName, p.AttachmentPath, p.AttachmentFileName
            FROM purchases p
            LEFT JOIN users u ON u.Id = p.CreatedBy
            WHERE p.CreatedAt >= @from AND p.CreatedAt <= @to
            ORDER BY p.Id DESC;
        ";
        cmd.Parameters.AddWithValue("@from", from.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@to", to.ToString("yyyy-MM-dd 23:59:59"));

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var purchase = new Purchase
            {
                Id = reader.GetInt32(0),
                InvoiceNumber = reader.GetString(1),
                ExternalInvoiceNumber = reader.IsDBNull(2) ? null : reader.GetString(2),
                SupplierName = reader.IsDBNull(3) ? null : reader.GetString(3),
                Notes = reader.IsDBNull(4) ? null : reader.GetString(4),
                Total = reader.GetDecimal(5),
                CreatedBy = reader.GetInt32(6),
                CreatedAt = DateTime.Parse(reader.GetString(7)),
                CreatorName = reader.IsDBNull(8) ? null : reader.GetString(8),
                AttachmentPath = reader.IsDBNull(9) ? null : reader.GetString(9),
                AttachmentFileName = reader.IsDBNull(10) ? null : reader.GetString(10)
            };
            purchases.Add(purchase);
        }

        return purchases;
    }

    public static decimal GetPurchasesTotalForRange(DateTime from, DateTime to)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COALESCE(SUM(Total), 0) FROM purchases
            WHERE CreatedAt >= @from AND CreatedAt <= @to;
        ";
        cmd.Parameters.AddWithValue("@from", from.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@to", to.ToString("yyyy-MM-dd 23:59:59"));

        return Convert.ToDecimal(cmd.ExecuteScalar());
    }

    public static Dictionary<string, decimal> GetDailyPurchasesTotal(DateTime from, DateTime to)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        var result = new Dictionary<string, decimal>();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT DATE(CreatedAt) AS d, COALESCE(SUM(Total), 0)
            FROM purchases
            WHERE CreatedAt >= @from AND CreatedAt <= @to
            GROUP BY DATE(CreatedAt)
            ORDER BY d;
        ";
        cmd.Parameters.AddWithValue("@from", from.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@to", to.ToString("yyyy-MM-dd 23:59:59"));

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result[reader.GetString(0)] = reader.GetDecimal(1);
        }

        return result;
    }

    public static Dictionary<int, decimal> GetMonthlyPurchasesTotal(int year)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        var result = new Dictionary<int, decimal>();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT CAST(strftime('%m', CreatedAt) AS INTEGER) AS m,
                   COALESCE(SUM(Total), 0)
            FROM purchases
            WHERE strftime('%Y', CreatedAt) = @year
            GROUP BY m
            ORDER BY m;
        ";
        cmd.Parameters.AddWithValue("@year", year.ToString());

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result[reader.GetInt32(0)] = reader.GetDecimal(1);
        }

        return result;
    }

    private static string GenerateInvoiceNumber(SqliteConnection connection)
    {
        var today = DateTime.Now.ToString("yyyyMMdd");
        var dateStr = DateTime.Now.ToString("yyyy-MM-dd");

        // Get or create counter
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO daily_counters (Date, LastInvoiceSeq, LastOrderNum, LastPurchaseSeq)
            VALUES (@date, 0, 0, 1)
            ON CONFLICT(Date) DO UPDATE SET LastPurchaseSeq = LastPurchaseSeq + 1
            RETURNING LastPurchaseSeq;
        ";
        cmd.Parameters.AddWithValue("@date", dateStr);
        var seq = Convert.ToInt32(cmd.ExecuteScalar());

        return $"PUR-{today}-{seq:D3}";
    }
}
