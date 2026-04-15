using ManufacturingERP.Domain.Common;

namespace ManufacturingERP.Domain.Entities;

public class BomHeader : BaseEntity
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public string Version { get; set; } = "v1";
    public ICollection<BomLine> Lines { get; set; } = new List<BomLine>();
}
