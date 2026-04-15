using ManufacturingERP.Domain.Common;

namespace ManufacturingERP.Domain.Entities;

public class Vehicle : BaseEntity
{
    public string VehicleNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DriverName { get; set; } = string.Empty;
    public string SalesRepName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
