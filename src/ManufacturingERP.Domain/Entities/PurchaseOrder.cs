using ManufacturingERP.Domain.Common;

namespace ManufacturingERP.Domain.Entities;

public class PurchaseOrder : BaseEntity
{
    public string OrderNo { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; } = DateTime.Today;
    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public string Status { get; set; } = "Draft";
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
}
