namespace CafePOS.Models;

public class ReturnItem
{
    public int Id { get; set; }
    public int ReturnId { get; set; }
    public int OrderItemId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public decimal Subtotal { get; set; }
}
