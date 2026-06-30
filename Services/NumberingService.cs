using CafePOS.Data;
using CafePOS.Models;
using Microsoft.Data.Sqlite;

namespace CafePOS.Services;

public static class NumberingService
{
    private static readonly object _lock = new();

    /// <summary>
    /// Generates both invoice number (INV-YYYYMMDD-XXX) and order number (1-999) atomically.
    /// Thread-safe with locking and SQLite transaction.
    /// </summary>
    public static (string InvoiceNumber, int OrderNumber) GenerateNumbers()
    {
        lock (_lock)
        {
            using var connection = DatabaseContext.GetConnection();
            connection.Open();

            using var transaction = connection.BeginTransaction();
            try
            {
                var today = DateTime.Now.ToString("yyyy-MM-dd");
                var todayCompact = DateTime.Now.ToString("yyyyMMdd");

                int invoiceSeq;
                int orderNum;

                // Check if today's counter exists
                using (var checkCmd = connection.CreateCommand())
                {
                    checkCmd.Transaction = transaction;
                    checkCmd.CommandText = "SELECT LastInvoiceSeq, LastOrderNum FROM daily_counters WHERE Date = @date;";
                    checkCmd.Parameters.AddWithValue("@date", today);

                    using var reader = checkCmd.ExecuteReader();
                    if (reader.Read())
                    {
                        invoiceSeq = reader.GetInt32(0) + 1;
                        orderNum = reader.GetInt32(1) + 1;

                        // Reset order number if exceeds 999
                        if (orderNum > 999)
                            orderNum = 1;
                    }
                    else
                    {
                        // First order of the day — start from 1
                        invoiceSeq = 1;
                        orderNum = 1;
                    }
                }

                // Upsert daily counter
                using (var upsertCmd = connection.CreateCommand())
                {
                    upsertCmd.Transaction = transaction;
                    upsertCmd.CommandText = @"
                        INSERT INTO daily_counters (Date, LastInvoiceSeq, LastOrderNum)
                        VALUES (@date, @seq, @num)
                        ON CONFLICT(Date) DO UPDATE SET
                            LastInvoiceSeq = @seq,
                            LastOrderNum = @num;
                    ";
                    upsertCmd.Parameters.AddWithValue("@date", today);
                    upsertCmd.Parameters.AddWithValue("@seq", invoiceSeq);
                    upsertCmd.Parameters.AddWithValue("@num", orderNum);
                    upsertCmd.ExecuteNonQuery();
                }

                transaction.Commit();

                var invoiceNumber = $"INV-{todayCompact}-{invoiceSeq:D3}";
                return (invoiceNumber, orderNum);
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }
}
