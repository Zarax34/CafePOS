namespace CafePOS.Models;

public class DailyCounter
{
    public string Date { get; set; } = string.Empty;
    public int LastInvoiceSeq { get; set; }
    public int LastOrderNum { get; set; }
}
