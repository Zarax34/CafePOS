namespace CafePOS.Models;

public class PurchaseItem
{
    public int Id { get; set; }
    public int PurchaseId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal CostPrice { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal Subtotal { get; set; }
}
