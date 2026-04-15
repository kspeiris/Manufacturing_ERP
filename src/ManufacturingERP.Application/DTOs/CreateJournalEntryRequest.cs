namespace ManufacturingERP.Application.DTOs;

public class CreateJournalEntryRequest
{
    public string Description { get; set; } = string.Empty;
    public List<CreateJournalLineRequest> Lines { get; set; } = new();
}

public class CreateJournalLineRequest
{
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}
