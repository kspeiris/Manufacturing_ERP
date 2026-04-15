using ManufacturingERP.Domain.Common;

namespace ManufacturingERP.Domain.Entities;

public class Supplier : BaseEntity
{
    public string SupplierCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ContactNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}
