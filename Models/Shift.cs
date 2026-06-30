namespace CafePOS.Models;

public class Shift
{
    public int Id { get; set; }
    public int CashierId { get; set; }
    public string? CashierName { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal ActualCash { get; set; }
    public decimal Difference { get; set; }
    public string Status { get; set; } = "open";
}
