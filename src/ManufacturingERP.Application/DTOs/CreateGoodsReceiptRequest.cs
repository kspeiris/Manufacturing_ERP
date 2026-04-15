namespace ManufacturingERP.Application.DTOs;

public class CreateGoodsReceiptRequest
{
    public int SupplierId { get; set; }
    public int WarehouseId { get; set; }
    public string? PurchaseOrderNo { get; set; }
    public string? Notes { get; set; }
    public List<CreateGoodsReceiptLineRequest> Items { get; set; } = new();
}

public class CreateGoodsReceiptLineRequest
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
}
