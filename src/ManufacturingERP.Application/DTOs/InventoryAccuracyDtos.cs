namespace ManufacturingERP.Application.DTOs;

public enum LotSelectionMethod
{
    Fifo = 0,
    WeightedAverage = 1
}

public class BatchLotSelectionDto
{
    public int BatchLotId { get; set; }
    public string LotNumber { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
    public decimal QuantitySelected { get; set; }
    public decimal UnitCost { get; set; }
}

public class StockReservationRequest
{
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public decimal Quantity { get; set; }
    public LotSelectionMethod SelectionMethod { get; set; } = LotSelectionMethod.Fifo;
}

public class ReorderAlertDto
{
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public decimal QuantityOnHand { get; set; }
    public decimal QuantityReserved { get; set; }
    public decimal QuantityAvailable { get; set; }
    public decimal ReorderLevel { get; set; }
}

public class CreateStockCountRequest
{
    public string? Notes { get; set; }
    public int WarehouseId { get; set; }
    public int? WarehouseBinId { get; set; }
    public int InitiatedByUserId { get; set; }
    public DateTime CountDate { get; set; } = DateTime.Today;
    public List<CreateStockCountLineRequest> Lines { get; set; } = new();
}

public class CreateStockCountLineRequest
{
    public int ProductId { get; set; }
    public int? BatchLotId { get; set; }
    public decimal BookQuantity { get; set; }
    public decimal CountedQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public string? Notes { get; set; }
}
