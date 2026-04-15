using ManufacturingERP.Domain.Common;

namespace ManufacturingERP.Domain.Entities;

public class PurchaseReturn : BaseEntity
{
    public string ReturnNo { get; set; } = string.Empty;
    public DateTime ReturnDate { get; set; } = DateTime.Today;
    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public string? ReferenceInvoiceNo { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Reason { get; set; }
    public ICollection<PurchaseReturnItem> Items { get; set; } = new List<PurchaseReturnItem>();
}
