using CafePOS.Data;

namespace CafePOS.Services;

public class SalesReport
{
    public decimal TotalRevenue { get; set; }
    public decimal TotalDiscounts { get; set; }
    public decimal TotalReturns { get; set; }
    public decimal NetCash { get; set; }
    public int OrderCount { get; set; }
}

public class TopProductItem
{
    public string Name { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public decimal TotalRevenue { get; set; }
}

public static class ReportService
{
    public static SalesReport GetDailySalesReport(DateTime date)
    {
        var from = date.Date.ToString("yyyy-MM-dd 00:00:00");
        var to = date.Date.ToString("yyyy-MM-dd 23:59:59");
        return GetSalesReportForRange(from, to);
    }

    public static SalesReport GetMonthlySalesReport(int year, int month)
    {
        var from = new DateTime(year, month, 1).ToString("yyyy-MM-dd 00:00:00");
        var to = new DateTime(year, month, DateTime.DaysInMonth(year, month)).ToString("yyyy-MM-dd 23:59:59");
        return GetSalesReportForRange(from, to);
    }

    private static SalesReport GetSalesReportForRange(string from, string to)
    {
        var report = new SalesReport();
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        // Get order totals
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT COUNT(*), COALESCE(SUM(Total), 0), COALESCE(SUM(DiscountAmount), 0)
                FROM orders WHERE CreatedAt >= @from AND CreatedAt <= @to;
            ";
            cmd.Parameters.AddWithValue("@from", from);
            cmd.Parameters.AddWithValue("@to", to);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                report.OrderCount = reader.GetInt32(0);
                report.TotalRevenue = (decimal)reader.GetDouble(1);
                report.TotalDiscounts = (decimal)reader.GetDouble(2);
            }
        }

        // Get returns total
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT COALESCE(SUM(TotalRefund), 0)
                FROM returns WHERE CreatedAt >= @from AND CreatedAt <= @to;
            ";
            cmd.Parameters.AddWithValue("@from", from);
            cmd.Parameters.AddWithValue("@to", to);

            var result = cmd.ExecuteScalar();
            report.TotalReturns = (decimal)Convert.ToDouble(result);
        }

        report.NetCash = report.TotalRevenue - report.TotalReturns;
        return report;
    }

    public static List<TopProductItem> GetTopProducts(DateTime from, DateTime to, int limit = 10)
    {
        var items = new List<TopProductItem>();
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT oi.ProductName, SUM(oi.Quantity) AS TotalQty, SUM(oi.Subtotal) AS TotalRev
            FROM order_items oi
            JOIN orders o ON oi.OrderId = o.Id
            WHERE o.CreatedAt >= @from AND o.CreatedAt <= @to
            GROUP BY oi.ProductName
            ORDER BY TotalQty DESC
            LIMIT @limit;
        ";
        cmd.Parameters.AddWithValue("@from", from.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@to", to.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new TopProductItem
            {
                Name = reader.GetString(0),
                TotalQuantity = reader.GetInt32(1),
                TotalRevenue = (decimal)reader.GetDouble(2)
            });
        }

        return items;
    }
}
