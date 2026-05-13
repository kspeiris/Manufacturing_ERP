using ManufacturingERP.Domain.Common;

namespace ManufacturingERP.Domain.Entities;

/// <summary>
/// Product line within a stock transfer, including dispatch and receipt variance.
/// </summary>
public class StockTransferLine : BaseEntity
{
    public int StockTransferId { get; set; }
    public StockTransfer? StockTransfer { get; set; }

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int? BatchLotId { get; set; }
    public BatchLot? BatchLot { get; set; }

    public decimal QuantityRequested { get; set; }
    public decimal QuantityDispatched { get; set; }
    public decimal QuantityReceived { get; set; }
    public string? Notes { get; set; }

    public decimal QuantityVariance => QuantityReceived - QuantityDispatched;
    public bool HasVariance => QuantityVariance != 0;
}
