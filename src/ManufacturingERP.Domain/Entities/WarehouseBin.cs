using ManufacturingERP.Domain.Common;

namespace ManufacturingERP.Domain.Entities;

/// <summary>
/// Physical storage bin, shelf, rack, or slot inside a warehouse.
/// </summary>
public class WarehouseBin : BaseEntity
{
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public string BinCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Aisle { get; set; }
    public string? Rack { get; set; }
    public string? Level { get; set; }
    public decimal MaxWeightKg { get; set; }
    public decimal MaxVolumeCbm { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPickable { get; set; } = true;
    public bool IsReceivable { get; set; } = true;
}
