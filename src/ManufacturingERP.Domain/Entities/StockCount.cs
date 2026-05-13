using ManufacturingERP.Domain.Common;
using ManufacturingERP.Domain.Enums;

namespace ManufacturingERP.Domain.Entities;

/// <summary>
/// Physical stock count used to reconcile book quantity with counted quantity.
/// </summary>
public class StockCount : BaseEntity
{
    public string CountNo { get; set; } = string.Empty;
    public StockCountStatus Status { get; set; } = StockCountStatus.Draft;
    public DateTime CountDate { get; set; } = DateTime.Today;
    public string? Notes { get; set; }

    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public int? WarehouseBinId { get; set; }
    public WarehouseBin? WarehouseBin { get; set; }

    public int InitiatedByUserId { get; set; }
    public User? InitiatedByUser { get; set; }
    public int? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }

    public int? VarianceVoucherId { get; set; }
    public Voucher? VarianceVoucher { get; set; }

    public ICollection<StockCountLine> Lines { get; set; } = new List<StockCountLine>();

    public int TotalLineCount => Lines.Count;
    public int VarianceLineCount => Lines.Count(l => l.HasVariance);
    public bool HasVariances => VarianceLineCount > 0;

    public void Start()
    {
        if (Status != StockCountStatus.Draft)
            throw new InvalidOperationException("Stock count has already been started.");

        Status = StockCountStatus.InProgress;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Submit()
    {
        if (Status != StockCountStatus.InProgress)
            throw new InvalidOperationException("Only in-progress counts can be submitted.");
        if (!Lines.Any())
            throw new InvalidOperationException("Count must have at least one line before submission.");

        Status = StockCountStatus.PendingApproval;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Approve(int byUserId, int? varianceVoucherId = null)
    {
        if (Status != StockCountStatus.PendingApproval)
            throw new InvalidOperationException("Only counts pending approval can be approved.");

        Status = StockCountStatus.Approved;
        ApprovedByUserId = byUserId;
        ApprovedAtUtc = DateTime.UtcNow;
        VarianceVoucherId = varianceVoucherId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status == StockCountStatus.Approved)
            throw new InvalidOperationException("Approved stock counts cannot be cancelled.");

        Status = StockCountStatus.Cancelled;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
