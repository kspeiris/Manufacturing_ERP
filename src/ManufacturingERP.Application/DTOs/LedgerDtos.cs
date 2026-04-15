namespace ManufacturingERP.Application.DTOs;

public class CustomerLedgerRowDto
{
    public DateTime EntryDate { get; set; }
    public string EntryType { get; set; } = string.Empty;
    public string ReferenceNo { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal RunningBalance { get; set; }
}

public class SupplierLedgerRowDto
{
    public DateTime EntryDate { get; set; }
    public string EntryType { get; set; } = string.Empty;
    public string ReferenceNo { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal RunningBalance { get; set; }
}

public class LedgerDateFilterDto
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
