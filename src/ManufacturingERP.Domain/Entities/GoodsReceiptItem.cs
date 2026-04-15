using ManufacturingERP.Domain.Common;

namespace ManufacturingERP.Domain.Entities;

public class GoodsReceiptItem : BaseEntity
{
    public int GoodsReceiptId { get; set; }
    public GoodsReceipt? GoodsReceipt { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal => Quantity * UnitCost;
}
