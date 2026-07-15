using CafePOS.Data;
using CafePOS.Models;

namespace CafePOS.Services;

public static class ShiftService
{
    public static Shift OpenShift(int cashierId)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO shifts (CashierId, StartTime, Status)
            VALUES (@cashier, @start, 'open');
            SELECT last_insert_rowid();
        ";
        cmd.Parameters.AddWithValue("@cashier", cashierId);
        cmd.Parameters.AddWithValue("@start", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        var id = Convert.ToInt32(cmd.ExecuteScalar());
        return new Shift
        {
            Id = id,
            CashierId = cashierId,
            StartTime = DateTime.Now,
            Status = "open"
        };
    }

    public static Shift? CloseShift(int shiftId, decimal actualCash)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        // Get shift info
        Shift? shift = null;
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT Id, CashierId, StartTime, Status FROM shifts WHERE Id = @id AND Status = 'open';";
            cmd.Parameters.AddWithValue("@id", shiftId);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            shift = new Shift
            {
                Id = reader.GetInt32(0),
                CashierId = reader.GetInt32(1),
                StartTime = DateTime.Parse(reader.GetString(2)),
                Status = reader.GetString(3)
            };
        }

        var endTime = DateTime.Now;

        // Calculate expected cash: total orders - total returns during shift
        decimal expectedCash = 0;
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT COALESCE(SUM(Total), 0) FROM orders
                WHERE CreatedAt >= @start AND CreatedAt <= @end;
            ";
            cmd.Parameters.AddWithValue("@start", shift.StartTime.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("@end", endTime.ToString("yyyy-MM-dd HH:mm:ss"));
            expectedCash = (decimal)Convert.ToDouble(cmd.ExecuteScalar());
        }

        decimal totalReturns = 0;
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT COALESCE(SUM(TotalRefund), 0) FROM returns
                WHERE CreatedAt >= @start AND CreatedAt <= @end;
            ";
            cmd.Parameters.AddWithValue("@start", shift.StartTime.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("@end", endTime.ToString("yyyy-MM-dd HH:mm:ss"));
            totalReturns = (decimal)Convert.ToDouble(cmd.ExecuteScalar());
        }

        decimal totalExpenses = 0;
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT COALESCE(SUM(Amount), 0) FROM expenses
                WHERE ShiftId = @shiftId;
            ";
            cmd.Parameters.AddWithValue("@shiftId", shiftId);
            totalExpenses = (decimal)Convert.ToDouble(cmd.ExecuteScalar());
        }

        expectedCash -= totalReturns + totalExpenses;
        var difference = actualCash - expectedCash;

        // Update shift
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                UPDATE shifts SET EndTime = @end, ExpectedCash = @expected,
                    ActualCash = @actual, Difference = @diff, Status = 'closed'
                WHERE Id = @id;
            ";
            cmd.Parameters.AddWithValue("@end", endTime.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("@expected", (double)expectedCash);
            cmd.Parameters.AddWithValue("@actual", (double)actualCash);
            cmd.Parameters.AddWithValue("@diff", (double)difference);
            cmd.Parameters.AddWithValue("@id", shiftId);
            cmd.ExecuteNonQuery();
        }

        shift.EndTime = endTime;
        shift.ExpectedCash = expectedCash;
        shift.ActualCash = actualCash;
        shift.Difference = difference;
        shift.Status = "closed";

        return shift;
    }

    public static Shift? GetCurrentShift(int cashierId)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT s.Id, s.CashierId, s.StartTime, s.EndTime, s.ExpectedCash,
                   s.ActualCash, s.Difference, s.Status, u.Username
            FROM shifts s
            LEFT JOIN users u ON s.CashierId = u.Id
            WHERE s.CashierId = @cashier AND s.Status = 'open'
            ORDER BY s.Id DESC LIMIT 1;
        ";
        cmd.Parameters.AddWithValue("@cashier", cashierId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return new Shift
        {
            Id = reader.GetInt32(0),
            CashierId = reader.GetInt32(1),
            StartTime = DateTime.Parse(reader.GetString(2)),
            EndTime = reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3)),
            ExpectedCash = (decimal)reader.GetDouble(4),
            ActualCash = (decimal)reader.GetDouble(5),
            Difference = (decimal)reader.GetDouble(6),
            Status = reader.GetString(7),
            CashierName = reader.IsDBNull(8) ? null : reader.GetString(8)
        };
    }

    public static List<Shift> GetShiftHistory()
    {
        var shifts = new List<Shift>();
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT s.Id, s.CashierId, s.StartTime, s.EndTime, s.ExpectedCash,
                   s.ActualCash, s.Difference, s.Status, u.Username
            FROM shifts s
            LEFT JOIN users u ON s.CashierId = u.Id
            ORDER BY s.Id DESC LIMIT 50;
        ";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            shifts.Add(new Shift
            {
                Id = reader.GetInt32(0),
                CashierId = reader.GetInt32(1),
                StartTime = DateTime.Parse(reader.GetString(2)),
                EndTime = reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3)),
                ExpectedCash = (decimal)reader.GetDouble(4),
                ActualCash = (decimal)reader.GetDouble(5),
                Difference = (decimal)reader.GetDouble(6),
                Status = reader.GetString(7),
                CashierName = reader.IsDBNull(8) ? null : reader.GetString(8)
            });
        }

        return shifts;
    }
}
