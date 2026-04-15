using ManufacturingERP.Domain.Common;

namespace ManufacturingERP.Domain.Entities;

public class VehicleLoadItem : BaseEntity
{
    public int VehicleLoadId { get; set; }
    public VehicleLoad? VehicleLoad { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public decimal QuantityLoaded { get; set; }
    public decimal QuantityReturned { get; set; }
}
