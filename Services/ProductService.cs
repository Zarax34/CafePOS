using CafePOS.Data;
using CafePOS.Models;

namespace CafePOS.Services;

public static class ProductService
{
    /// <summary>
    /// Gets all active products with category names.
    /// </summary>
    public static List<Product> GetAllProducts()
    {
        var products = new List<Product>();
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT p.Id, p.Name, p.Price, p.CategoryId, c.Name AS CategoryName,
                   p.ImagePath, p.IsActive, p.SortOrder, p.IsPurchaseOnly
            FROM products p
            LEFT JOIN categories c ON p.CategoryId = c.Id
            WHERE p.IsActive = 1 AND p.IsPurchaseOnly = 0
            ORDER BY p.SortOrder, p.Name;
        ";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            products.Add(new Product
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Price = (decimal)reader.GetDouble(2),
                CategoryId = reader.GetInt32(3),
                CategoryName = reader.IsDBNull(4) ? null : reader.GetString(4),
                ImagePath = reader.IsDBNull(5) ? null : reader.GetString(5),
                IsActive = reader.GetInt32(6) == 1,
                SortOrder = reader.GetInt32(7),
                IsPurchaseOnly = reader.GetInt32(8) == 1
            });
        }

        return products;
    }

    /// <summary>
    /// Gets products filtered by category.
    /// </summary>
    public static List<Product> GetProductsByCategory(int categoryId)
    {
        var products = new List<Product>();
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT p.Id, p.Name, p.Price, p.CategoryId, c.Name AS CategoryName,
                   p.ImagePath, p.IsActive, p.SortOrder, p.IsPurchaseOnly
            FROM products p
            LEFT JOIN categories c ON p.CategoryId = c.Id
            WHERE p.IsActive = 1 AND p.IsPurchaseOnly = 0 AND p.CategoryId = @catId
            ORDER BY p.SortOrder, p.Name;
        ";
        cmd.Parameters.AddWithValue("@catId", categoryId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            products.Add(new Product
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Price = (decimal)reader.GetDouble(2),
                CategoryId = reader.GetInt32(3),
                CategoryName = reader.IsDBNull(4) ? null : reader.GetString(4),
                ImagePath = reader.IsDBNull(5) ? null : reader.GetString(5),
                IsActive = reader.GetInt32(6) == 1,
                SortOrder = reader.GetInt32(7),
                IsPurchaseOnly = reader.GetInt32(8) == 1
            });
        }

        return products;
    }

    /// <summary>
    /// Gets all products including inactive (for manager).
    /// </summary>
    public static List<Product> GetAllProductsAdmin()
    {
        var products = new List<Product>();
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT p.Id, p.Name, p.Price, p.CategoryId, c.Name AS CategoryName,
                   p.ImagePath, p.IsActive, p.SortOrder, p.IsPurchaseOnly
            FROM products p
            LEFT JOIN categories c ON p.CategoryId = c.Id
            WHERE p.IsPurchaseOnly = 0
            ORDER BY p.SortOrder, p.Name;
        ";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            products.Add(new Product
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Price = (decimal)reader.GetDouble(2),
                CategoryId = reader.GetInt32(3),
                CategoryName = reader.IsDBNull(4) ? null : reader.GetString(4),
                ImagePath = reader.IsDBNull(5) ? null : reader.GetString(5),
                IsActive = reader.GetInt32(6) == 1,
                SortOrder = reader.GetInt32(7),
                IsPurchaseOnly = reader.GetInt32(8) == 1
            });
        }

        return products;
    }

    /// <summary>
    /// Gets ALL active products including purchase-only (for PurchasesView).
    /// </summary>
    public static List<Product> GetAllPurchaseProducts()
    {
        var products = new List<Product>();
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT p.Id, p.Name, p.Price, p.CategoryId, c.Name AS CategoryName,
                   p.ImagePath, p.IsActive, p.SortOrder, p.IsPurchaseOnly
            FROM products p
            LEFT JOIN categories c ON p.CategoryId = c.Id
            WHERE p.IsActive = 1
            ORDER BY p.SortOrder, p.Name;
        ";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            products.Add(new Product
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Price = (decimal)reader.GetDouble(2),
                CategoryId = reader.GetInt32(3),
                CategoryName = reader.IsDBNull(4) ? null : reader.GetString(4),
                ImagePath = reader.IsDBNull(5) ? null : reader.GetString(5),
                IsActive = reader.GetInt32(6) == 1,
                SortOrder = reader.GetInt32(7),
                IsPurchaseOnly = reader.GetInt32(8) == 1
            });
        }

        return products;
    }

    public static Product AddProduct(Product p)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO products (Name, Price, CategoryId, ImagePath, IsActive, SortOrder, IsPurchaseOnly)
            VALUES (@name, @price, @catId, @img, @active, @sort, @purchaseOnly);
            SELECT last_insert_rowid();
        ";
        cmd.Parameters.AddWithValue("@name", p.Name);
        cmd.Parameters.AddWithValue("@price", (double)p.Price);
        cmd.Parameters.AddWithValue("@catId", p.CategoryId);
        cmd.Parameters.AddWithValue("@img", (object?)p.ImagePath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@active", p.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("@sort", p.SortOrder);
        cmd.Parameters.AddWithValue("@purchaseOnly", p.IsPurchaseOnly ? 1 : 0);

        p.Id = Convert.ToInt32(cmd.ExecuteScalar());
        return p;
    }

    public static bool UpdateProduct(Product p)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE products SET Name = @name, Price = @price, CategoryId = @catId,
                ImagePath = @img, IsActive = @active, SortOrder = @sort,
                IsPurchaseOnly = @purchaseOnly
            WHERE Id = @id;
        ";
        cmd.Parameters.AddWithValue("@name", p.Name);
        cmd.Parameters.AddWithValue("@price", (double)p.Price);
        cmd.Parameters.AddWithValue("@catId", p.CategoryId);
        cmd.Parameters.AddWithValue("@img", (object?)p.ImagePath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@active", p.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("@sort", p.SortOrder);
        cmd.Parameters.AddWithValue("@purchaseOnly", p.IsPurchaseOnly ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", p.Id);

        return cmd.ExecuteNonQuery() > 0;
    }

    public static bool DeleteProduct(int id)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM products WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        return cmd.ExecuteNonQuery() > 0;
    }

    public static bool ToggleProductActive(int id)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE products SET IsActive = CASE WHEN IsActive = 1 THEN 0 ELSE 1 END WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        return cmd.ExecuteNonQuery() > 0;
    }

    // --- Categories ---

    public static List<Category> GetAllCategories()
    {
        var categories = new List<Category>();
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, SortOrder, IsActive FROM categories WHERE IsActive = 1 ORDER BY SortOrder, Name;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            categories.Add(new Category
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                SortOrder = reader.GetInt32(2),
                IsActive = reader.GetInt32(3) == 1
            });
        }

        return categories;
    }

    public static Category AddCategory(Category c)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO categories (Name, SortOrder, IsActive)
            VALUES (@name, @sort, @active);
            SELECT last_insert_rowid();
        ";
        cmd.Parameters.AddWithValue("@name", c.Name);
        cmd.Parameters.AddWithValue("@sort", c.SortOrder);
        cmd.Parameters.AddWithValue("@active", c.IsActive ? 1 : 0);

        c.Id = Convert.ToInt32(cmd.ExecuteScalar());
        return c;
    }

    public static bool UpdateCategory(Category c)
    {
        using var connection = DatabaseContext.GetConnection();
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE categories SET Name = @name, SortOrder = @sort, IsActive = @active WHERE Id = @id;";
        cmd.Parameters.AddWithValue("@name", c.Name);
        cmd.Parameters.AddWithValue("@sort", c.SortOrder);
        cmd.Parameters.AddWithValue("@active", c.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", c.Id);

        return cmd.ExecuteNonQuery() > 0;
    }
}
