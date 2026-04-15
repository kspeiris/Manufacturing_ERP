using ManufacturingERP.Domain.Common;

namespace ManufacturingERP.Domain.Entities;

public class ProductCategory : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
