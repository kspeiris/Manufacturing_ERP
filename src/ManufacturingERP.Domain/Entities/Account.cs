using ManufacturingERP.Domain.Common;

namespace ManufacturingERP.Domain.Entities;

public class Account : BaseEntity
{
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
