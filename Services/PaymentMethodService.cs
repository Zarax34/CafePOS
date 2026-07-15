using CafePOS.Data;
using CafePOS.Models;

namespace CafePOS.Services;

public static class PaymentMethodService
{
    public static List<PaymentMethod> GetAll()
    {
        var methods = new List<PaymentMethod>();
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name FROM payment_methods ORDER BY Id;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            methods.Add(new PaymentMethod
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1)
            });
        }

        return methods;
    }

    public static void Add(string name)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO payment_methods (Name) VALUES (@name);";
        cmd.Parameters.AddWithValue("@name", name);
        cmd.ExecuteNonQuery();
    }

    public static void Delete(int id)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM payment_methods WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }
}
