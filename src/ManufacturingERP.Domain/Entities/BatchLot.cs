using ManufacturingERP.Domain.Common;

namespace ManufacturingERP.Domain.Entities;

/// <summary>
/// Tracks a product batch or lot for expiry management and FEFO stock picking.
/// </summary>
public class BatchLot : BaseEntity
{
    public string LotNumber { get; set; } = string.Empty;

    public int ProductId { get; set; }
    public Product? Product { get; set; }

    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public int? WarehouseBinId { get; set; }
    public WarehouseBin? WarehouseBin { get; set; }

    public DateTime ManufacturingDate { get; set; }
    public DateTime? ExpiryDate { get; set; }

    public decimal QuantityReceived { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal QuantityReserved { get; set; }
    public string? SourceDocument { get; set; }
    public bool IsActive { get; set; } = true;

    public decimal QuantityAvailable => QuantityOnHand - QuantityReserved;
    public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value.Date < DateTime.Today;

    public bool IsExpiringSoon(int warningDays = 30) =>
        ExpiryDate.HasValue &&
        !IsExpired &&
        ExpiryDate.Value.Date <= DateTime.Today.AddDays(warningDays);

    public void Reserve(decimal quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive.", nameof(quantity));
        if (quantity > QuantityAvailable)
            throw new InvalidOperationException($"Insufficient available stock in lot {LotNumber}. Available: {QuantityAvailable}.");

        QuantityReserved += quantity;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Consume(decimal quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive.", nameof(quantity));
        if (quantity > QuantityOnHand)
            throw new InvalidOperationException($"Cannot consume {quantity}; only {QuantityOnHand} on hand in lot {LotNumber}.");

        QuantityReserved = Math.Max(0, QuantityReserved - quantity);
        QuantityOnHand -= quantity;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
