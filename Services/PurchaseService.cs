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
                INSERT INTO purchases (InvoiceNumber, SupplierName, Notes, Total, CreatedBy, CreatedAt)
                VALUES (@inv, @supplier, @notes, @total, @createdBy, @createdAt);
                SELECT last_insert_rowid();
            ";
            cmd.Parameters.AddWithValue("@inv", purchase.InvoiceNumber);
            cmd.Parameters.AddWithValue("@supplier", (object?)purchase.SupplierName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@notes", (object?)purchase.Notes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@total", purchase.Total);
            cmd.Parameters.AddWithValue("@createdBy", purchase.CreatedBy);
            cmd.Parameters.AddWithValue("@createdAt", purchase.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));

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

    public static List<Purchase> GetPurchasesByDateRange(DateTime from, DateTime to)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        var purchases = new List<Purchase>();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT p.Id, p.InvoiceNumber, p.SupplierName, p.Notes, p.Total, p.CreatedBy, p.CreatedAt,
                   u.FullName AS CreatorName
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
                SupplierName = reader.IsDBNull(2) ? null : reader.GetString(2),
                Notes = reader.IsDBNull(3) ? null : reader.GetString(3),
                Total = reader.GetDecimal(4),
                CreatedBy = reader.GetInt32(5),
                CreatedAt = DateTime.Parse(reader.GetString(6)),
                CreatorName = reader.IsDBNull(7) ? null : reader.GetString(7)
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
