using ManufacturingERP.Domain.Common;

namespace ManufacturingERP.Domain.Entities;

public class ExpenseEntry : BaseEntity
{
    public DateTime ExpenseDate { get; set; } = DateTime.Today;
    public string ExpenseType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ReferenceNo { get; set; }
}
