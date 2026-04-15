using ManufacturingERP.Domain.Common;

namespace ManufacturingERP.Domain.Entities;

public class GoodsReceipt : BaseEntity
{
    public string ReceiptNo { get; set; } = string.Empty;
    public DateTime ReceiptDate { get; set; } = DateTime.Today;
    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public string? PurchaseOrderNo { get; set; }
    public string? Notes { get; set; }
    public decimal TotalAmount { get; set; }
    public ICollection<GoodsReceiptItem> Items { get; set; } = new List<GoodsReceiptItem>();
}
