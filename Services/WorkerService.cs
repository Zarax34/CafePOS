using CafePOS.Data;
using CafePOS.Models;

namespace CafePOS.Services;

public static class WorkerService
{
    public static List<Worker> GetAll()
    {
        var workers = new List<Worker>();
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, Name, Phone, IsActive, CreatedAt
            FROM workers
            WHERE IsActive = 1
            ORDER BY Name;
        ";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            workers.Add(new Worker
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Phone = reader.IsDBNull(2) ? null : reader.GetString(2),
                IsActive = reader.GetInt32(3) == 1,
                CreatedAt = DateTime.Parse(reader.GetString(4))
            });
        }

        return workers;
    }

    public static Worker Add(string name, string? phone)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO workers (Name, Phone, IsActive, CreatedAt)
            VALUES (@name, @phone, 1, @createdAt);
            SELECT last_insert_rowid();
        ";
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@phone", (object?)phone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@createdAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        return new Worker
        {
            Id = Convert.ToInt32(cmd.ExecuteScalar()),
            Name = name,
            Phone = phone,
            IsActive = true,
            CreatedAt = DateTime.Now
        };
    }

    public static void Delete(int id)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE workers SET IsActive = 0 WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }
}
