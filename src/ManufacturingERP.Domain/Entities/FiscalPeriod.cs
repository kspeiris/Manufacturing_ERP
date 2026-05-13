using ManufacturingERP.Domain.Common;
using ManufacturingERP.Domain.Enums;

namespace ManufacturingERP.Domain.Entities;

/// <summary>
/// Represents an accounting period. Transactions cannot be posted to closed or locked periods.
/// </summary>
public class FiscalPeriod : BaseEntity
{
    public int FiscalYear { get; set; }
    public int PeriodNumber { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public FiscalPeriodStatus Status { get; set; } = FiscalPeriodStatus.Open;

    public int? ClosedByUserId { get; set; }
    public User? ClosedByUser { get; set; }
    public DateTime? ClosedAtUtc { get; set; }

    public bool IsOpen => Status == FiscalPeriodStatus.Open;

    public void Close(int byUserId)
    {
        if (Status == FiscalPeriodStatus.Locked)
            throw new InvalidOperationException("Period is locked and cannot be changed.");

        Status = FiscalPeriodStatus.Closed;
        ClosedByUserId = byUserId;
        ClosedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Lock(int byUserId)
    {
        Status = FiscalPeriodStatus.Locked;
        ClosedByUserId = byUserId;
        ClosedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Reopen()
    {
        if (Status == FiscalPeriodStatus.Locked)
            throw new InvalidOperationException("Locked periods cannot be reopened without an admin override.");

        Status = FiscalPeriodStatus.Open;
        ClosedByUserId = null;
        ClosedAtUtc = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
