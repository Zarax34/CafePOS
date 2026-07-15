namespace CafePOS.Models;

public class Expense
{
    public int Id { get; set; }
    public string? InvoiceNumber { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public int CashierId { get; set; }
    public int ShiftId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string CashierName { get; set; } = "";

    public string ExpenseType { get; set; } = "سداد";
    public int? WorkerId { get; set; }
    public string? WorkerName { get; set; }
}
