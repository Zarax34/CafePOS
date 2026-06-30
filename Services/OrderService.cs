using CafePOS.Data;
using CafePOS.Models;

namespace CafePOS.Services;

public static class OrderService
{
    /// <summary>
    /// Creates an order with generated dual numbers, saves order + items in a transaction.
    /// </summary>
    public static Order CreateOrder(Order order)
    {
        var (invoiceNumber, orderNumber) = NumberingService.GenerateNumbers();

        order.InvoiceNumber = invoiceNumber;
        order.OrderNumber = orderNumber;
        order.CreatedAt = DateTime.Now;

        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();
        try
        {
            // Insert order
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO orders (InvoiceNumber, OrderNumber, CustomerName, Subtotal,
                        DiscountPercent, DiscountAmount, Total, CashierId, CreatedAt)
                    VALUES (@inv, @ord, @cust, @sub, @dp, @da, @total, @cashier, @created);
                    SELECT last_insert_rowid();
                ";
                cmd.Parameters.AddWithValue("@inv", order.InvoiceNumber);
                cmd.Parameters.AddWithValue("@ord", order.OrderNumber);
                cmd.Parameters.AddWithValue("@cust", (object?)order.CustomerName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@sub", (double)order.Subtotal);
                cmd.Parameters.AddWithValue("@dp", (double)order.DiscountPercent);
                cmd.Parameters.AddWithValue("@da", (double)order.DiscountAmount);
                cmd.Parameters.AddWithValue("@total", (double)order.Total);
                cmd.Parameters.AddWithValue("@cashier", order.CashierId);
                cmd.Parameters.AddWithValue("@created", order.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));

                order.Id = Convert.ToInt32(cmd.ExecuteScalar());
            }

            // Insert order items
            foreach (var item in order.Items)
            {
                item.OrderId = order.Id;
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO order_items (OrderId, ProductId, ProductName, Price, Quantity, Subtotal)
                    VALUES (@orderId, @prodId, @prodName, @price, @qty, @sub);
                    SELECT last_insert_rowid();
                ";
                cmd.Parameters.AddWithValue("@orderId", item.OrderId);
                cmd.Parameters.AddWithValue("@prodId", item.ProductId);
                cmd.Parameters.AddWithValue("@prodName", item.ProductName);
                cmd.Parameters.AddWithValue("@price", (double)item.Price);
                cmd.Parameters.AddWithValue("@qty", item.Quantity);
                cmd.Parameters.AddWithValue("@sub", (double)item.Subtotal);

                item.Id = Convert.ToInt32(cmd.ExecuteScalar());
            }

            transaction.Commit();
            return order;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// Gets an order by invoice number, including all items.
    /// </summary>
    public static Order? GetOrderByInvoice(string invoiceNumber)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        Order? order = null;

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT o.Id, o.InvoiceNumber, o.OrderNumber, o.CustomerName, o.Subtotal,
                    o.DiscountPercent, o.DiscountAmount, o.Total, o.CashierId, o.CreatedAt,
                    u.Username || CASE WHEN u.FullName IS NOT NULL AND u.FullName <> '' THEN ' - ' || u.FullName ELSE '' END
                FROM orders o
                LEFT JOIN users u ON o.CashierId = u.Id
                WHERE o.InvoiceNumber = @inv;
            ";
            cmd.Parameters.AddWithValue("@inv", invoiceNumber);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                order = new Order
                {
                    Id = reader.GetInt32(0),
                    InvoiceNumber = reader.GetString(1),
                    OrderNumber = reader.GetInt32(2),
                    CustomerName = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Subtotal = (decimal)reader.GetDouble(4),
                    DiscountPercent = (decimal)reader.GetDouble(5),
                    DiscountAmount = (decimal)reader.GetDouble(6),
                    Total = (decimal)reader.GetDouble(7),
                    CashierId = reader.GetInt32(8),
                    CreatedAt = DateTime.Parse(reader.GetString(9)),
                    CashierName = reader.IsDBNull(10) ? null : reader.GetString(10)
                };
            }
        }

        if (order == null) return null;

        // Load items
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT Id, OrderId, ProductId, ProductName, Price, Quantity, Subtotal
                FROM order_items WHERE OrderId = @orderId;
            ";
            cmd.Parameters.AddWithValue("@orderId", order.Id);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                order.Items.Add(new OrderItem
                {
                    Id = reader.GetInt32(0),
                    OrderId = reader.GetInt32(1),
                    ProductId = reader.GetInt32(2),
                    ProductName = reader.GetString(3),
                    Price = (decimal)reader.GetDouble(4),
                    Quantity = reader.GetInt32(5),
                    Subtotal = (decimal)reader.GetDouble(6)
                });
            }
        }

        return order;
    }

    /// <summary>
    /// Gets all orders for today.
    /// </summary>
    public static List<Order> GetTodayOrders()
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        return GetOrdersByDateRange(
            DateTime.Parse(today),
            DateTime.Parse(today).AddDays(1).AddSeconds(-1)
        );
    }

    /// <summary>
    /// Gets orders within a date range.
    /// </summary>
    public static List<Order> GetOrdersByDateRange(DateTime from, DateTime to)
    {
        var orders = new List<Order>();
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT o.Id, o.InvoiceNumber, o.OrderNumber, o.CustomerName, o.Subtotal,
                o.DiscountPercent, o.DiscountAmount, o.Total, o.CashierId, o.CreatedAt,
                u.Username || CASE WHEN u.FullName IS NOT NULL AND u.FullName <> '' THEN ' - ' || u.FullName ELSE '' END
            FROM orders o
            LEFT JOIN users u ON o.CashierId = u.Id
            WHERE o.CreatedAt >= @from AND o.CreatedAt <= @to
            ORDER BY o.Id DESC;
        ";
        cmd.Parameters.AddWithValue("@from", from.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@to", to.ToString("yyyy-MM-dd HH:mm:ss"));

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            orders.Add(new Order
            {
                Id = reader.GetInt32(0),
                InvoiceNumber = reader.GetString(1),
                OrderNumber = reader.GetInt32(2),
                CustomerName = reader.IsDBNull(3) ? null : reader.GetString(3),
                Subtotal = (decimal)reader.GetDouble(4),
                DiscountPercent = (decimal)reader.GetDouble(5),
                DiscountAmount = (decimal)reader.GetDouble(6),
                Total = (decimal)reader.GetDouble(7),
                CashierId = reader.GetInt32(8),
                CreatedAt = DateTime.Parse(reader.GetString(9)),
                CashierName = reader.IsDBNull(10) ? null : reader.GetString(10)
            });
        }

        return orders;
    }

    /// <summary>
    /// Searches orders by invoice number or customer name.
    /// </summary>
    public static List<Order> SearchOrders(string query)
    {
        var orders = new List<Order>();
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT o.Id, o.InvoiceNumber, o.OrderNumber, o.CustomerName, o.Subtotal,
                o.DiscountPercent, o.DiscountAmount, o.Total, o.CashierId, o.CreatedAt,
                u.Username || CASE WHEN u.FullName IS NOT NULL AND u.FullName <> '' THEN ' - ' || u.FullName ELSE '' END
            FROM orders o
            LEFT JOIN users u ON o.CashierId = u.Id
            WHERE o.InvoiceNumber LIKE @query OR o.CustomerName LIKE @query
            ORDER BY o.Id DESC
            LIMIT 100;
        ";
        cmd.Parameters.AddWithValue("@query", $"%{query}%");

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            orders.Add(new Order
            {
                Id = reader.GetInt32(0),
                InvoiceNumber = reader.GetString(1),
                OrderNumber = reader.GetInt32(2),
                CustomerName = reader.IsDBNull(3) ? null : reader.GetString(3),
                Subtotal = (decimal)reader.GetDouble(4),
                DiscountPercent = (decimal)reader.GetDouble(5),
                DiscountAmount = (decimal)reader.GetDouble(6),
                Total = (decimal)reader.GetDouble(7),
                CashierId = reader.GetInt32(8),
                CreatedAt = DateTime.Parse(reader.GetString(9)),
                CashierName = reader.IsDBNull(10) ? null : reader.GetString(10)
            });
        }
        return orders;
    }
}
