namespace CafePOS.Models;

public class Return
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public decimal TotalRefund { get; set; }
    public int CashierId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public List<ReturnItem> Items { get; set; } = new();
}
