using CafePOS.Data;
using CafePOS.Models;

namespace CafePOS.Services;

public static class SettingsService
{
    public static string GetSetting(string key)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Value FROM settings WHERE Key = @key;";
        cmd.Parameters.AddWithValue("@key", key);

        var result = cmd.ExecuteScalar();
        return result?.ToString() ?? string.Empty;
    }

    public static void SetSetting(string key, string value)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO settings (Key, Value) VALUES (@key, @value)
            ON CONFLICT(Key) DO UPDATE SET Value = @value;
        ";
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", value);
        cmd.ExecuteNonQuery();
    }

    public static Dictionary<string, string> GetAllSettings()
    {
        var settings = new Dictionary<string, string>();
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Key, Value FROM settings;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            settings[reader.GetString(0)] = reader.GetString(1);
        }

        return settings;
    }

    public static bool IsDiscountEnabled()
    {
        return GetSetting("discount_enabled") == "1";
    }

    public static decimal GetDiscountPercent()
    {
        var val = GetSetting("discount_percent");
        return decimal.TryParse(val, out var percent) ? percent : 0m;
    }
}
