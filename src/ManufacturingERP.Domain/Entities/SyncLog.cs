using ManufacturingERP.Domain.Common;

namespace ManufacturingERP.Domain.Entities;

public class SyncLog : BaseEntity
{
    public string SyncType { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public DateTime SyncStartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SyncCompletedAt { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Notes { get; set; }
}
