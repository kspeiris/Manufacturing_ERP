namespace ManufacturingERP.Application.DTOs;

public class LedgerSummaryRowDto
{
    public string PartyCode { get; set; } = string.Empty;
    public string PartyName { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public string LedgerType { get; set; } = string.Empty;
}
