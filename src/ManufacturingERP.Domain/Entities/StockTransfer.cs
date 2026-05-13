using ManufacturingERP.Domain.Common;
using ManufacturingERP.Domain.Enums;

namespace ManufacturingERP.Domain.Entities;

/// <summary>
/// Records movement of goods between warehouses. Approval is required before stock is adjusted.
/// </summary>
public class StockTransfer : BaseEntity
{
    public string TransferNo { get; set; } = string.Empty;
    public StockTransferStatus Status { get; set; } = StockTransferStatus.Draft;
    public DateTime TransferDate { get; set; } = DateTime.Today;
    public string? Notes { get; set; }

    public int FromWarehouseId { get; set; }
    public Warehouse? FromWarehouse { get; set; }
    public int? FromBinId { get; set; }
    public WarehouseBin? FromBin { get; set; }

    public int ToWarehouseId { get; set; }
    public Warehouse? ToWarehouse { get; set; }
    public int? ToBinId { get; set; }
    public WarehouseBin? ToBin { get; set; }

    public int RequestedByUserId { get; set; }
    public User? RequestedByUser { get; set; }
    public int? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public int? ReceivedByUserId { get; set; }
    public User? ReceivedByUser { get; set; }
    public DateTime? ReceivedAtUtc { get; set; }

    public ICollection<StockTransferLine> Lines { get; set; } = new List<StockTransferLine>();

    public void Submit()
    {
        if (Status != StockTransferStatus.Draft)
            throw new InvalidOperationException("Only Draft transfers can be submitted.");
        if (!Lines.Any())
            throw new InvalidOperationException("Transfer must have at least one line.");

        Status = StockTransferStatus.PendingApproval;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Approve(int byUserId)
    {
        if (Status != StockTransferStatus.PendingApproval)
            throw new InvalidOperationException("Only transfers pending approval can be approved.");

        Status = StockTransferStatus.Approved;
        ApprovedByUserId = byUserId;
        ApprovedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Dispatch()
    {
        if (Status != StockTransferStatus.Approved)
            throw new InvalidOperationException("Transfer must be approved before dispatching.");

        Status = StockTransferStatus.InTransit;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Receive(int byUserId)
    {
        if (Status != StockTransferStatus.InTransit)
            throw new InvalidOperationException("Only in-transit transfers can be received.");

        Status = StockTransferStatus.Received;
        ReceivedByUserId = byUserId;
        ReceivedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status == StockTransferStatus.Received)
            throw new InvalidOperationException("Completed transfers cannot be cancelled.");

        Status = StockTransferStatus.Cancelled;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
