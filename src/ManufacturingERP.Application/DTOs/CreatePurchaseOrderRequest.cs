namespace ManufacturingERP.Application.DTOs;

public class CreatePurchaseOrderRequest
{
    public int SupplierId { get; set; }
    public string? Notes { get; set; }
    public List<CreatePurchaseOrderLineRequest> Items { get; set; } = new();
}

public class CreatePurchaseOrderLineRequest
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
