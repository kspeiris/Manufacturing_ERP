using ManufacturingERP.Domain.Common;

namespace ManufacturingERP.Domain.Entities;

/// <summary>
/// A single debit or credit line within a voucher.
/// </summary>
public class VoucherLine : BaseEntity
{
    public int VoucherId { get; set; }
    public Voucher? Voucher { get; set; }

    public int AccountId { get; set; }
    public Account? Account { get; set; }

    public string Description { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }

    public int? TaxId { get; set; }
    public Tax? Tax { get; set; }
    public decimal TaxAmount { get; set; }
    public string? CostCenter { get; set; }

    public decimal NetAmount => (Debit + Credit) - TaxAmount;
}
