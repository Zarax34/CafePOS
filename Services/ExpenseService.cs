using CafePOS.Data;
using CafePOS.Models;

namespace CafePOS.Services;

public static class ExpenseService
{
    public static Expense AddExpense(Expense expense)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();
        using var tx = connection.BeginTransaction();

        var today = DateTime.Now.ToString("yyyy-MM-dd");
        var dateStr = DateTime.Now.ToString("yyyyMMdd");

        // Get or create daily counter
        using (var counterCmd = connection.CreateCommand())
        {
            counterCmd.CommandText = @"
                INSERT INTO daily_counters (Date, LastInvoiceSeq, LastOrderNum, LastPurchaseSeq, LastExpenseSeq)
                VALUES (@date, 0, 0, 0, 1)
                ON CONFLICT(Date) DO UPDATE SET LastExpenseSeq = LastExpenseSeq + 1;
            ";
            counterCmd.Parameters.AddWithValue("@date", today);
            counterCmd.ExecuteNonQuery();
        }

        // Read back the sequence
        int seq;
        using (var readCmd = connection.CreateCommand())
        {
            readCmd.CommandText = "SELECT LastExpenseSeq FROM daily_counters WHERE Date = @date;";
            readCmd.Parameters.AddWithValue("@date", today);
            seq = Convert.ToInt32(readCmd.ExecuteScalar());
        }

        expense.InvoiceNumber = $"EXP-{dateStr}-{seq:D3}";

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO expenses (InvoiceNumber, Description, Amount, CashierId, ShiftId, CreatedAt, ExpenseType, WorkerId)
            VALUES (@invNum, @desc, @amount, @cashierId, @shiftId, @createdAt, @expenseType, @workerId);
            SELECT last_insert_rowid();
        ";
        cmd.Parameters.AddWithValue("@invNum", expense.InvoiceNumber);
        cmd.Parameters.AddWithValue("@desc", expense.Description);
        cmd.Parameters.AddWithValue("@amount", (double)expense.Amount);
        cmd.Parameters.AddWithValue("@cashierId", expense.CashierId);
        cmd.Parameters.AddWithValue("@shiftId", expense.ShiftId);
        cmd.Parameters.AddWithValue("@createdAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@expenseType", expense.ExpenseType);
        cmd.Parameters.AddWithValue("@workerId", (object?)expense.WorkerId ?? DBNull.Value);

        expense.Id = Convert.ToInt32(cmd.ExecuteScalar());
        expense.CreatedAt = DateTime.Now;

        tx.Commit();
        return expense;
    }

    public static List<Expense> GetAllExpenses(DateTime? from = null, DateTime? to = null)
    {
        var expenses = new List<Expense>();
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        var sql = @"
            SELECT e.Id, e.InvoiceNumber, e.Description, e.Amount, e.CashierId, e.ShiftId, e.CreatedAt,
                   COALESCE(u.FullName, u.Username) AS CashierName,
                   e.ExpenseType, e.WorkerId, COALESCE(w.Name, '') AS WorkerName
            FROM expenses e
            LEFT JOIN users u ON e.CashierId = u.Id
            LEFT JOIN workers w ON e.WorkerId = w.Id
            WHERE 1=1
        ";

        if (from.HasValue)
            sql += " AND e.CreatedAt >= @from";
        if (to.HasValue)
            sql += " AND e.CreatedAt <= @to";

        sql += " ORDER BY e.CreatedAt DESC;";

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        if (from.HasValue)
            cmd.Parameters.AddWithValue("@from", from.Value.ToString("yyyy-MM-dd HH:mm:ss"));
        if (to.HasValue)
            cmd.Parameters.AddWithValue("@to", to.Value.ToString("yyyy-MM-dd HH:mm:ss"));

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            expenses.Add(new Expense
            {
                Id = reader.GetInt32(0),
                InvoiceNumber = reader.IsDBNull(1) ? null : reader.GetString(1),
                Description = reader.GetString(2),
                Amount = (decimal)reader.GetDouble(3),
                CashierId = reader.GetInt32(4),
                ShiftId = reader.GetInt32(5),
                CreatedAt = DateTime.Parse(reader.GetString(6)),
                CashierName = reader.GetString(7),
                ExpenseType = reader.IsDBNull(8) ? "سداد" : reader.GetString(8),
                WorkerId = reader.IsDBNull(9) ? null : reader.GetInt32(9),
                WorkerName = reader.IsDBNull(10) ? null : reader.GetString(10)
            });
        }

        return expenses;
    }

    public static List<Expense> GetExpensesByShift(int shiftId)
    {
        var expenses = new List<Expense>();
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT e.Id, e.Description, e.Amount, e.CashierId, e.ShiftId, e.CreatedAt,
                   COALESCE(u.FullName, u.Username) AS CashierName,
                   e.ExpenseType, e.WorkerId, COALESCE(w.Name, '') AS WorkerName
            FROM expenses e
            LEFT JOIN users u ON e.CashierId = u.Id
            LEFT JOIN workers w ON e.WorkerId = w.Id
            WHERE e.ShiftId = @shiftId
            ORDER BY e.CreatedAt DESC;
        ";
        cmd.Parameters.AddWithValue("@shiftId", shiftId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            expenses.Add(new Expense
            {
                Id = reader.GetInt32(0),
                InvoiceNumber = reader.IsDBNull(1) ? null : reader.GetString(1),
                Description = reader.GetString(2),
                Amount = (decimal)reader.GetDouble(3),
                CashierId = reader.GetInt32(4),
                ShiftId = reader.GetInt32(5),
                CreatedAt = DateTime.Parse(reader.GetString(6)),
                CashierName = reader.GetString(7),
                ExpenseType = reader.IsDBNull(8) ? "سداد" : reader.GetString(8),
                WorkerId = reader.IsDBNull(9) ? null : reader.GetInt32(9),
                WorkerName = reader.IsDBNull(10) ? null : reader.GetString(10)
            });
        }

        return expenses;
    }

    public static Expense? GetExpenseByInvoice(string invoiceNumber)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT e.Id, e.InvoiceNumber, e.Description, e.Amount, e.CashierId, e.ShiftId, e.CreatedAt,
                   COALESCE(u.FullName, u.Username) AS CashierName,
                   e.ExpenseType, e.WorkerId, COALESCE(w.Name, '') AS WorkerName
            FROM expenses e
            LEFT JOIN users u ON e.CashierId = u.Id
            LEFT JOIN workers w ON e.WorkerId = w.Id
            WHERE e.InvoiceNumber = @invNum;
        ";
        cmd.Parameters.AddWithValue("@invNum", invoiceNumber);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new Expense
            {
                Id = reader.GetInt32(0),
                InvoiceNumber = reader.IsDBNull(1) ? null : reader.GetString(1),
                Description = reader.GetString(2),
                Amount = (decimal)reader.GetDouble(3),
                CashierId = reader.GetInt32(4),
                ShiftId = reader.GetInt32(5),
                CreatedAt = DateTime.Parse(reader.GetString(6)),
                CashierName = reader.GetString(7),
                ExpenseType = reader.IsDBNull(8) ? "سداد" : reader.GetString(8),
                WorkerId = reader.IsDBNull(9) ? null : reader.GetInt32(9),
                WorkerName = reader.IsDBNull(10) ? null : reader.GetString(10)
            };
        }
        return null;
    }

    public static List<Expense> GetWorkerWithdrawals(int workerId, DateTime? from = null, DateTime? to = null)
    {
        var expenses = new List<Expense>();
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        var sql = @"
            SELECT e.Id, e.InvoiceNumber, e.Description, e.Amount, e.CashierId, e.ShiftId, e.CreatedAt,
                   COALESCE(u.FullName, u.Username) AS CashierName,
                   e.ExpenseType, e.WorkerId, COALESCE(w.Name, '') AS WorkerName
            FROM expenses e
            LEFT JOIN users u ON e.CashierId = u.Id
            LEFT JOIN workers w ON e.WorkerId = w.Id
            WHERE e.ExpenseType = 'سحبيات العمال' AND e.WorkerId = @workerId
        ";
        if (from.HasValue)
            sql += " AND e.CreatedAt >= @from";
        if (to.HasValue)
            sql += " AND e.CreatedAt <= @to";

        sql += " ORDER BY e.CreatedAt DESC;";

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@workerId", workerId);

        if (from.HasValue)
            cmd.Parameters.AddWithValue("@from", from.Value.ToString("yyyy-MM-dd HH:mm:ss"));
        if (to.HasValue)
            cmd.Parameters.AddWithValue("@to", to.Value.ToString("yyyy-MM-dd HH:mm:ss"));

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            expenses.Add(new Expense
            {
                Id = reader.GetInt32(0),
                InvoiceNumber = reader.IsDBNull(1) ? null : reader.GetString(1),
                Description = reader.GetString(2),
                Amount = (decimal)reader.GetDouble(3),
                CashierId = reader.GetInt32(4),
                ShiftId = reader.GetInt32(5),
                CreatedAt = DateTime.Parse(reader.GetString(6)),
                CashierName = reader.GetString(7),
                ExpenseType = reader.IsDBNull(8) ? "سداد" : reader.GetString(8),
                WorkerId = reader.IsDBNull(9) ? null : reader.GetInt32(9),
                WorkerName = reader.IsDBNull(10) ? null : reader.GetString(10)
            });
        }

        return expenses;
    }

    public static List<WorkerWithdrawalSummary> GetWorkerWithdrawalSummary(DateTime? from = null, DateTime? to = null)
    {
        var summaries = new List<WorkerWithdrawalSummary>();
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        var sql = @"
            SELECT w.Id, w.Name, COALESCE(SUM(e.Amount), 0), COUNT(e.Id)
            FROM workers w
            LEFT JOIN expenses e ON e.WorkerId = w.Id AND e.ExpenseType = 'سحبيات العمال'
            WHERE w.IsActive = 1
        ";
        if (from.HasValue)
            sql += " AND (e.CreatedAt IS NULL OR e.CreatedAt >= @from)";
        if (to.HasValue)
            sql += " AND (e.CreatedAt IS NULL OR e.CreatedAt <= @to)";

        sql += " GROUP BY w.Id, w.Name ORDER BY w.Name;";

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        if (from.HasValue)
            cmd.Parameters.AddWithValue("@from", from.Value.ToString("yyyy-MM-dd HH:mm:ss"));
        if (to.HasValue)
            cmd.Parameters.AddWithValue("@to", to.Value.ToString("yyyy-MM-dd HH:mm:ss"));

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            summaries.Add(new WorkerWithdrawalSummary
            {
                WorkerId = reader.GetInt32(0),
                WorkerName = reader.GetString(1),
                TotalAmount = (decimal)reader.GetDouble(2),
                WithdrawalCount = reader.GetInt32(3)
            });
        }

        return summaries;
    }

    public static void DeleteExpense(int id)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM expenses WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public static decimal GetTotalExpenses(DateTime? from, DateTime? to)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        var sql = "SELECT COALESCE(SUM(Amount), 0) FROM expenses WHERE 1=1";

        if (from.HasValue)
            sql += " AND CreatedAt >= @from";
        if (to.HasValue)
            sql += " AND CreatedAt <= @to";

        sql += ";";

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;

        if (from.HasValue)
            cmd.Parameters.AddWithValue("@from", from.Value.ToString("yyyy-MM-dd HH:mm:ss"));
        if (to.HasValue)
            cmd.Parameters.AddWithValue("@to", to.Value.ToString("yyyy-MM-dd HH:mm:ss"));

        return (decimal)Convert.ToDouble(cmd.ExecuteScalar());
    }
}

public class WorkerWithdrawalSummary
{
    public int WorkerId { get; set; }
    public string WorkerName { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public int WithdrawalCount { get; set; }
}
