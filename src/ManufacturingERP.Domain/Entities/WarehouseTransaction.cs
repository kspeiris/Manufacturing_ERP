using ManufacturingERP.Domain.Common;

namespace ManufacturingERP.Domain.Entities;

public class WarehouseTransaction : BaseEntity
{
    public DateTime TransactionDate { get; set; } = DateTime.Now;
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public decimal QuantityIn { get; set; }
    public decimal QuantityOut { get; set; }
    public string ReferenceNo { get; set; } = string.Empty;
    public string? Remarks { get; set; }
}
