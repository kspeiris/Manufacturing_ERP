using ManufacturingERP.Domain.Common;

namespace ManufacturingERP.Domain.Entities;

public class Product : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int ProductCategoryId { get; set; }
    public ProductCategory? ProductCategory { get; set; }
    public string Unit { get; set; } = "PCS";
    public decimal CostPrice { get; set; }
    public decimal SellingPrice { get; set; }
    public decimal ReorderLevel { get; set; }
    public bool TrackBatch { get; set; }
    public bool IsActive { get; set; } = true;
}
