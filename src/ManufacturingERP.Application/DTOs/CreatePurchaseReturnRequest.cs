namespace ManufacturingERP.Application.DTOs;

public class CreatePurchaseReturnRequest
{
    public int SupplierId { get; set; }
    public int WarehouseId { get; set; }
    public string? ReferenceInvoiceNo { get; set; }
    public string? Reason { get; set; }
    public List<CreatePurchaseReturnLineRequest> Items { get; set; } = new();
}

public class CreatePurchaseReturnLineRequest
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
}
