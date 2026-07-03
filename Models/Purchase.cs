namespace CafePOS.Models;

public class Purchase
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string? SupplierName { get; set; }
    public string? Notes { get; set; }
    public decimal Total { get; set; }
    public int CreatedBy { get; set; }
    public string? CreatorName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public List<PurchaseItem> Items { get; set; } = new();
}
