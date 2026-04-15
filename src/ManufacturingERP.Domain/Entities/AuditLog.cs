using ManufacturingERP.Domain.Common;

namespace ManufacturingERP.Domain.Entities;

public class AuditLog : BaseEntity
{
    public int? UserId { get; set; }
    public User? User { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityKey { get; set; } = string.Empty;
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public DateTime ActionAtUtc { get; set; } = DateTime.UtcNow;
}
