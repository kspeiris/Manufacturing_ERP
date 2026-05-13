using ManufacturingERP.Domain.Common;
using ManufacturingERP.Domain.Enums;

namespace ManufacturingERP.Domain.Entities;

/// <summary>
/// Represents a tax rule such as VAT, WHT, or fixed excise duty.
/// </summary>
public class Tax : BaseEntity
{
    public string TaxCode { get; set; } = string.Empty;
    public string TaxName { get; set; } = string.Empty;
    public TaxType TaxType { get; set; } = TaxType.Percentage;
    public decimal Rate { get; set; }

    public int? InputTaxAccountId { get; set; }
    public Account? InputTaxAccount { get; set; }

    public int? OutputTaxAccountId { get; set; }
    public Account? OutputTaxAccount { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }

    public decimal Calculate(decimal baseAmountOrQty)
    {
        return TaxType switch
        {
            TaxType.Percentage => Math.Round(baseAmountOrQty * Rate / 100, 2),
            TaxType.FixedAmount => Math.Round(baseAmountOrQty * Rate, 2),
            _ => 0m
        };
    }
}
