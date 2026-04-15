namespace ManufacturingERP.Application.DTOs;

public class SaveBomRequest
{
    public int FinishedProductId { get; set; }
    public string Version { get; set; } = "V1";
    public List<SaveBomLineRequest> Lines { get; set; } = new();
}

public class SaveBomLineRequest
{
    public int MaterialProductId { get; set; }
    public decimal QuantityRequired { get; set; }
}

public class ReceiveFinishedGoodsRequest
{
    public int ProductionOrderId { get; set; }
    public int WarehouseId { get; set; }
    public decimal ProducedQuantity { get; set; }
    public decimal ScrapQuantity { get; set; }
    public string? BatchNo { get; set; }
}

public class ProductionLedgerRowDto
{
    public DateTime EntryDate { get; set; }
    public string EntryType { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
    public string ReferenceNo { get; set; } = string.Empty;
    public string? Remarks { get; set; }
}
