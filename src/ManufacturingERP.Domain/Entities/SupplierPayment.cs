using ManufacturingERP.Domain.Common;

namespace ManufacturingERP.Domain.Entities;

public class SupplierPayment : BaseEntity
{
    public string PaymentNo { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; } = DateTime.Today;
    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public string? ReferenceInvoiceNo { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string? Notes { get; set; }
}
