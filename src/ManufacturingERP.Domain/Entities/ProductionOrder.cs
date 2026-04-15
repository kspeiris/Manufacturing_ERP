using ManufacturingERP.Domain.Common;

namespace ManufacturingERP.Domain.Entities;

public class ProductionOrder : BaseEntity
{
    public string OrderNo { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public int FinishedProductId { get; set; }
    public Product? FinishedProduct { get; set; }
    public decimal PlannedQuantity { get; set; }
    public decimal ProducedQuantity { get; set; }
    public decimal ScrapQuantity { get; set; }
    public decimal MaterialCost { get; set; }
    public decimal LaborCost { get; set; }
    public decimal OverheadCost { get; set; }
    public string BatchNo { get; set; } = string.Empty;
    public decimal TotalCost => MaterialCost + LaborCost + OverheadCost;
    public decimal UnitCost => ProducedQuantity == 0 ? 0 : TotalCost / ProducedQuantity;
    public string Status { get; set; } = "Planned";
}
