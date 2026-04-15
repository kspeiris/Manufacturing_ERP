using ManufacturingERP.Domain.Common;

namespace ManufacturingERP.Domain.Entities;

public class RoutePlan : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Territory { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}
