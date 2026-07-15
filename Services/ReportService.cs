using CafePOS.Data;

namespace CafePOS.Services;

public class SalesReport
{
    public decimal TotalRevenue { get; set; }
    public decimal TotalDiscounts { get; set; }
    public decimal TotalReturns { get; set; }
    public decimal TotalPurchases { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetCash => TotalRevenue - TotalReturns - TotalExpenses;
    public decimal NetProfit => NetCash - TotalPurchases;
    public int OrderCount { get; set; }
}

public class DailyBreakdownItem
{
    public string Date { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal Purchases { get; set; }
    public decimal ShiftCloseCash { get; set; }
}

public class MonthlyBreakdownItem
{
    public string Month { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalDiscounts { get; set; }
    public decimal TotalReturns { get; set; }
    public decimal TotalPurchases { get; set; }
    public decimal NetCash { get; set; }
}

public class TopProductItem
{
    public string Name { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class PaymentMethodBreakdownItem
{
    public string PaymentMethod { get; set; } = string.Empty;
    public int OrderCount { get; set; }
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

    public static SalesReport Get30DaySalesReport(DateTime startDate)
    {
        var from = startDate.Date.ToString("yyyy-MM-dd 00:00:00");
        var to = startDate.Date.AddDays(29).ToString("yyyy-MM-dd 23:59:59");
        return GetSalesReportForRange(from, to);
    }

    public static SalesReport GetMonthlySalesReport(int year, int month)
    {
        var from = new DateTime(year, month, 1).ToString("yyyy-MM-dd 00:00:00");
        var to = new DateTime(year, month, DateTime.DaysInMonth(year, month)).ToString("yyyy-MM-dd 23:59:59");
        return GetSalesReportForRange(from, to);
    }

    /// <summary>
    /// Returns the total purchases for the given date range.
    /// </summary>
    public static decimal GetPurchasesTotal(string from, string to)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(SUM(Total), 0) FROM purchases WHERE CreatedAt >= @from AND CreatedAt <= @to;";
        cmd.Parameters.AddWithValue("@from", from);
        cmd.Parameters.AddWithValue("@to", to);
        return (decimal)Convert.ToDouble(cmd.ExecuteScalar());
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

        // Get purchases total
        report.TotalPurchases = GetPurchasesTotal(from, to);

        // Get expenses total
        report.TotalExpenses = GetExpensesTotal(from, to);

        return report;
    }

    public static List<DailyBreakdownItem> GetDailyBreakdown(string fromDate, string toDate)
    {
        var items = new List<DailyBreakdownItem>();
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT o.Day, o.OrderCount, o.TotalRevenue,
                   COALESCE(p.PurchaseTotal, 0) AS PurchaseTotal,
                   COALESCE(s.ActualCash, 0) AS ShiftCash
            FROM (
                SELECT substr(CreatedAt,1,10) AS Day,
                       COUNT(*) AS OrderCount,
                       COALESCE(SUM(Total), 0) AS TotalRevenue
                FROM orders
                WHERE CreatedAt >= @from AND CreatedAt <= @to
                GROUP BY substr(CreatedAt,1,10)
            ) o
            LEFT JOIN (
                SELECT substr(CreatedAt,1,10) AS Day,
                       SUM(Total) AS PurchaseTotal
                FROM purchases
                WHERE CreatedAt >= @from AND CreatedAt <= @to
                GROUP BY substr(CreatedAt,1,10)
            ) p ON o.Day = p.Day
            LEFT JOIN (
                SELECT substr(EndTime,1,10) AS Day,
                       ActualCash
                FROM shifts
                WHERE Status = 'closed'
                  AND EndTime >= @from AND EndTime <= @to
            ) s ON o.Day = s.Day
            ORDER BY o.Day ASC;
        ";
        cmd.Parameters.AddWithValue("@from", fromDate);
        cmd.Parameters.AddWithValue("@to", toDate);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new DailyBreakdownItem
            {
                Date = reader.GetString(0),
                OrderCount = reader.GetInt32(1),
                TotalRevenue = (decimal)reader.GetDouble(2),
                Purchases = (decimal)reader.GetDouble(3),
                ShiftCloseCash = (decimal)reader.GetDouble(4)
            });
        }

        return items;
    }

    public static List<MonthlyBreakdownItem> GetYearlyBreakdown(int year)
    {
        var items = new List<MonthlyBreakdownItem>();
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        var from = $"{year}-01-01 00:00:00";
        var to = $"{year}-12-31 23:59:59";

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT substr(CreatedAt,1,7) AS Month,
                   COUNT(*) AS OrderCount,
                   COALESCE(SUM(Total), 0) AS TotalRevenue,
                   COALESCE(SUM(DiscountAmount), 0) AS TotalDiscounts
            FROM orders
            WHERE CreatedAt >= @from AND CreatedAt <= @to
            GROUP BY substr(CreatedAt,1,7)
            ORDER BY Month ASC;
        ";
        cmd.Parameters.AddWithValue("@from", from);
        cmd.Parameters.AddWithValue("@to", to);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var month = reader.GetString(0);
            var totalReturns = 0m;
            var totalPurchases = 0m;

            using (var rcmd = connection.CreateCommand())
            {
                rcmd.CommandText = @"
                    SELECT COALESCE(SUM(TotalRefund), 0)
                    FROM returns
                    WHERE substr(CreatedAt,1,7) = @month;
                ";
                rcmd.Parameters.AddWithValue("@month", month);
                var r = rcmd.ExecuteScalar();
                totalReturns = (decimal)Convert.ToDouble(r);
            }

            using (var pcmd = connection.CreateCommand())
            {
                pcmd.CommandText = @"
                    SELECT COALESCE(SUM(Total), 0)
                    FROM purchases
                    WHERE substr(CreatedAt,1,7) = @month;
                ";
                pcmd.Parameters.AddWithValue("@month", month);
                var p = pcmd.ExecuteScalar();
                totalPurchases = (decimal)Convert.ToDouble(p);
            }

            items.Add(new MonthlyBreakdownItem
            {
                Month = month,
                OrderCount = reader.GetInt32(1),
                TotalRevenue = (decimal)reader.GetDouble(2),
                TotalDiscounts = (decimal)reader.GetDouble(3),
                TotalReturns = totalReturns,
                TotalPurchases = totalPurchases,
                NetCash = (decimal)reader.GetDouble(2) - totalReturns
            });
        }

        return items;
    }

    public static decimal GetExpensesTotal(string from, string to)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(SUM(Amount), 0) FROM expenses WHERE CreatedAt >= @from AND CreatedAt <= @to;";
        cmd.Parameters.AddWithValue("@from", from);
        cmd.Parameters.AddWithValue("@to", to);
        return (decimal)Convert.ToDouble(cmd.ExecuteScalar());
    }

    public static List<TopProductItem> GetTopProducts(DateTime from, DateTime to, int? limit = null)
    {
        var items = new List<TopProductItem>();
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        var sql = @"
            SELECT oi.ProductName, SUM(oi.Quantity) AS TotalQty, SUM(oi.Subtotal) AS TotalRev
            FROM order_items oi
            JOIN orders o ON oi.OrderId = o.Id
            WHERE o.CreatedAt >= @from AND o.CreatedAt <= @to
            GROUP BY oi.ProductName
            ORDER BY TotalQty DESC";
        if (limit.HasValue)
            sql += " LIMIT @limit;";
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@from", from.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@to", to.ToString("yyyy-MM-dd HH:mm:ss"));
        if (limit.HasValue)
            cmd.Parameters.AddWithValue("@limit", limit.Value);

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

    public static List<PaymentMethodBreakdownItem> GetPaymentMethodBreakdown(string from, string to)
    {
        var items = new List<PaymentMethodBreakdownItem>();
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT COALESCE(o.PaymentMethod, 'غير محدد') AS Method,
                   COUNT(*) AS OrderCount,
                   COALESCE(SUM(o.Total), 0) AS TotalRevenue
            FROM orders o
            WHERE o.CreatedAt >= @from AND o.CreatedAt <= @to
            GROUP BY o.PaymentMethod
            ORDER BY TotalRevenue DESC;
        ";
        cmd.Parameters.AddWithValue("@from", from);
        cmd.Parameters.AddWithValue("@to", to);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            items.Add(new PaymentMethodBreakdownItem
            {
                PaymentMethod = reader.GetString(0),
                OrderCount = reader.GetInt32(1),
                TotalRevenue = (decimal)reader.GetDouble(2)
            });
        }

        return items;
    }
}
