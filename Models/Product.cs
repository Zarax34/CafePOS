namespace CafePOS.Models;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? ImagePath { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
