using ManufacturingERP.Domain.Common;

namespace ManufacturingERP.Domain.Entities;

public class BomLine : BaseEntity
{
    public int BomHeaderId { get; set; }
    public BomHeader? BomHeader { get; set; }
    public int MaterialProductId { get; set; }
    public Product? MaterialProduct { get; set; }
    public decimal QuantityRequired { get; set; }
}
