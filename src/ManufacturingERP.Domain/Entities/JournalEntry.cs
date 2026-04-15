using ManufacturingERP.Domain.Common;

namespace ManufacturingERP.Domain.Entities;

public class JournalEntry : BaseEntity
{
    public string EntryNo { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; } = DateTime.Today;
    public string Description { get; set; } = string.Empty;
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public ICollection<JournalLine> Lines { get; set; } = new List<JournalLine>();
}
