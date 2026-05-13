using ManufacturingERP.Domain.Enums;

namespace ManufacturingERP.Application.DTOs;

public class CreateVoucherRequest
{
    public string Description { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public VoucherType VoucherType { get; set; } = VoucherType.JournalVoucher;
    public DateTime VoucherDate { get; set; } = DateTime.Today;
    public List<CreateVoucherLineRequest> Lines { get; set; } = new();
}

public class CreateVoucherLineRequest
{
    public string AccountCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public int? TaxId { get; set; }
    public string? CostCenter { get; set; }
}

public class TaxCalculationDto
{
    public int TaxId { get; set; }
    public string TaxCode { get; set; } = string.Empty;
    public string TaxName { get; set; } = string.Empty;
    public decimal TaxAmount { get; set; }
}
