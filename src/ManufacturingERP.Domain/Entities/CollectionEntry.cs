using ManufacturingERP.Domain.Common;

namespace ManufacturingERP.Domain.Entities;

public class CollectionEntry : BaseEntity
{
    public DateTime CollectionDate { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public decimal Amount { get; set; }
    public string ReferenceNo { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}
