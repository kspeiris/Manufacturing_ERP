using ManufacturingERP.Domain.Common;
using ManufacturingERP.Domain.Enums;

namespace ManufacturingERP.Domain.Entities;

public class SalesInvoice : BaseEntity
{
    public string InvoiceNo { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    public SaleType SaleType { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public ICollection<SalesInvoiceItem> Items { get; set; } = new List<SalesInvoiceItem>();
}
