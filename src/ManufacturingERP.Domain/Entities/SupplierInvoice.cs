using ManufacturingERP.Domain.Common;

namespace ManufacturingERP.Domain.Entities;

public class SupplierInvoice : BaseEntity
{
    public string InvoiceNo { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; } = DateTime.Today;
    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public string? ReferencePoNo { get; set; }
    public string? ReferenceGrnNo { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal BalanceAmount { get; set; }
    public DateTime? DueDate { get; set; }
    public string Status { get; set; } = "Open";
    public string? Notes { get; set; }
}
