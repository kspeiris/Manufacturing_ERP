using ManufacturingERP.Domain.Common;

namespace ManufacturingERP.Domain.Entities;

public class ProductionMaterialIssue : BaseEntity
{
    public int ProductionOrderId { get; set; }
    public ProductionOrder? ProductionOrder { get; set; }
    public int MaterialProductId { get; set; }
    public Product? MaterialProduct { get; set; }
    public decimal QuantityIssued { get; set; }
    public DateTime IssueDate { get; set; } = DateTime.Now;
}
