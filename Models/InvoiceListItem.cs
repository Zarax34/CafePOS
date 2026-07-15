namespace CafePOS.Models;

public enum InvoiceType { Sale, Purchase, Expense }

public class InvoiceListItem
{
    public required string InvoiceNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public required string PersonName { get; set; }
    public decimal Total { get; set; }
    public InvoiceType Type { get; set; }
    public string? ExternalInvoiceNumber { get; set; }
    public string? Description { get; set; }
    public string TypeLabel => Type switch
    {
        InvoiceType.Sale => "مبيعات",
        InvoiceType.Purchase => "مشتريات",
        InvoiceType.Expense => "مصروفات",
        _ => ""
    };
    public string TypeBadgeColor => Type switch
    {
        InvoiceType.Sale => "#1A73E8",
        InvoiceType.Purchase => "#E67E22",
        InvoiceType.Expense => "#E74C3C",
        _ => "#999"
    };
}
