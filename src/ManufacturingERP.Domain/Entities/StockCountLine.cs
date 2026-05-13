using ManufacturingERP.Domain.Common;

namespace ManufacturingERP.Domain.Entities;

/// <summary>
/// Product line in a stock count, including quantity and value variance.
/// </summary>
public class StockCountLine : BaseEntity
{
    public int StockCountId { get; set; }
    public StockCount? StockCount { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int? BatchLotId { get; set; }
    public BatchLot? BatchLot { get; set; }

    public decimal BookQuantity { get; set; }
    public decimal CountedQuantity { get; set; }
    public decimal UnitCost { get; set; }
    public string? Notes { get; set; }

    public decimal VarianceQuantity => CountedQuantity - BookQuantity;
    public bool HasVariance => VarianceQuantity != 0;
    public decimal VarianceValue => VarianceQuantity * UnitCost;
}
