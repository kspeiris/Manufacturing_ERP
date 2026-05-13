namespace ManufacturingERP.Domain.Enums;

public enum StockTransferStatus
{
    Draft = 0,
    PendingApproval = 1,
    Approved = 2,
    InTransit = 3,
    Received = 4,
    Cancelled = 5
}
