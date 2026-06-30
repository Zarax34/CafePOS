namespace CafePOS.Models;

public class Order
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public int OrderNumber { get; set; }
    public string? CustomerName { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Total { get; set; }
    public int CashierId { get; set; }
    public string? CashierName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public List<OrderItem> Items { get; set; } = new();
}
