using CafePOS.Data;
using CafePOS.Models;

namespace CafePOS.Services;

public static class ReturnService
{
    /// <summary>
    /// Processes a return: inserts return + items in a transaction.
    /// </summary>
    public static Return ProcessReturn(Return ret)
    {
        ret.CreatedAt = DateTime.Now;

        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();
        try
        {
            // Insert return record
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO returns (OrderId, InvoiceNumber, Reason, TotalRefund, CashierId, CreatedAt)
                    VALUES (@orderId, @inv, @reason, @refund, @cashier, @created);
                    SELECT last_insert_rowid();
                ";
                cmd.Parameters.AddWithValue("@orderId", ret.OrderId);
                cmd.Parameters.AddWithValue("@inv", ret.InvoiceNumber);
                cmd.Parameters.AddWithValue("@reason", ret.Reason);
                cmd.Parameters.AddWithValue("@refund", (double)ret.TotalRefund);
                cmd.Parameters.AddWithValue("@cashier", ret.CashierId);
                cmd.Parameters.AddWithValue("@created", ret.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));

                ret.Id = Convert.ToInt32(cmd.ExecuteScalar());
            }

            // Insert return items
            foreach (var item in ret.Items)
            {
                item.ReturnId = ret.Id;
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO return_items (ReturnId, OrderItemId, ProductName, Price, Quantity, Subtotal)
                    VALUES (@retId, @itemId, @name, @price, @qty, @sub);
                    SELECT last_insert_rowid();
                ";
                cmd.Parameters.AddWithValue("@retId", item.ReturnId);
                cmd.Parameters.AddWithValue("@itemId", item.OrderItemId);
                cmd.Parameters.AddWithValue("@name", item.ProductName);
                cmd.Parameters.AddWithValue("@price", (double)item.Price);
                cmd.Parameters.AddWithValue("@qty", item.Quantity);
                cmd.Parameters.AddWithValue("@sub", (double)item.Subtotal);

                item.Id = Convert.ToInt32(cmd.ExecuteScalar());
            }

            transaction.Commit();
            return ret;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public static List<Return> GetTodayReturns()
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        return GetReturnsByDateRange(
            DateTime.Parse(today),
            DateTime.Parse(today).AddDays(1).AddSeconds(-1)
        );
    }

    public static List<Return> GetReturnsByDateRange(DateTime from, DateTime to)
    {
        var returns = new List<Return>();
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, OrderId, InvoiceNumber, Reason, TotalRefund, CashierId, CreatedAt
            FROM returns
            WHERE CreatedAt >= @from AND CreatedAt <= @to
            ORDER BY Id DESC;
        ";
        cmd.Parameters.AddWithValue("@from", from.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@to", to.ToString("yyyy-MM-dd HH:mm:ss"));

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            returns.Add(new Return
            {
                Id = reader.GetInt32(0),
                OrderId = reader.GetInt32(1),
                InvoiceNumber = reader.GetString(2),
                Reason = reader.GetString(3),
                TotalRefund = (decimal)reader.GetDouble(4),
                CashierId = reader.GetInt32(5),
                CreatedAt = DateTime.Parse(reader.GetString(6))
            });
        }

        return returns;
    }

    public static List<Return> GetReturnsByOrderId(int orderId)
    {
        var returns = new List<Return>();
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT r.Id, r.OrderId, r.InvoiceNumber, r.Reason, r.TotalRefund, r.CashierId, r.CreatedAt
            FROM returns r WHERE r.OrderId = @orderId ORDER BY r.Id DESC;
        ";
        cmd.Parameters.AddWithValue("@orderId", orderId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var ret = new Return
            {
                Id = reader.GetInt32(0),
                OrderId = reader.GetInt32(1),
                InvoiceNumber = reader.GetString(2),
                Reason = reader.GetString(3),
                TotalRefund = (decimal)reader.GetDouble(4),
                CashierId = reader.GetInt32(5),
                CreatedAt = DateTime.Parse(reader.GetString(6))
            };

            // Load return items
            using var itemCmd = connection.CreateCommand();
            itemCmd.CommandText = "SELECT Id, ReturnId, OrderItemId, ProductName, Price, Quantity, Subtotal FROM return_items WHERE ReturnId = @retId;";
            itemCmd.Parameters.AddWithValue("@retId", ret.Id);

            using var itemReader = itemCmd.ExecuteReader();
            while (itemReader.Read())
            {
                ret.Items.Add(new ReturnItem
                {
                    Id = itemReader.GetInt32(0),
                    ReturnId = itemReader.GetInt32(1),
                    OrderItemId = itemReader.GetInt32(2),
                    ProductName = itemReader.GetString(3),
                    Price = (decimal)itemReader.GetDouble(4),
                    Quantity = itemReader.GetInt32(5),
                    Subtotal = (decimal)itemReader.GetDouble(6)
                });
            }

            returns.Add(ret);
        }

        return returns;
    }
}
