using ManufacturingERP.Domain.Common;

namespace ManufacturingERP.Domain.Entities;

public class VehicleLoad : BaseEntity
{
    public DateTime LoadDate { get; set; }
    public int VehicleId { get; set; }
    public Vehicle? Vehicle { get; set; }
    public string RouteName { get; set; } = string.Empty;
    public ICollection<VehicleLoadItem> Items { get; set; } = new List<VehicleLoadItem>();
}
