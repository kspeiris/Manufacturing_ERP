using ManufacturingERP.Application.Abstractions;
using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace ManufacturingERP.Application.Services;

public class WarehouseService
{
    private readonly IAppDbContext _db;
    private readonly AuthorizationService _authorizationService;
    private readonly AuditService _auditService;
    private readonly CurrentUserService _currentUserService;

    public WarehouseService(IAppDbContext db, AuthorizationService authorizationService, AuditService auditService, CurrentUserService currentUserService)
    {
        _db = db;
        _authorizationService = authorizationService;
        _auditService = auditService;
        _currentUserService = currentUserService;
    }

    public async Task<List<StockRowDto>> GetStockAsync()
    {
        return await _db.StockBalances
            .Include(x => x.Product)
            .Include(x => x.Warehouse)
            .Where(x => x.Product != null && x.Warehouse != null)
            .OrderBy(x => x.Product!.Name)
            .Select(x => new StockRowDto
            {
                ProductCode = x.Product!.Code,
                ProductName = x.Product!.Name,
                WarehouseName = x.Warehouse!.Name,
                QuantityOnHand = x.QuantityOnHand,
                CostPrice = x.Product!.CostPrice
            })
            .ToListAsync();
    }

    public async Task<List<StockMovementRowDto>> GetStockMovementsAsync(int? productId = null, int? warehouseId = null)
    {
        var query = _db.WarehouseTransactions
            .Include(x => x.Product)
            .Include(x => x.Warehouse)
            .Where(x => x.Product != null && x.Warehouse != null);

        if (productId.HasValue)
            query = query.Where(x => x.ProductId == productId.Value);

        if (warehouseId.HasValue)
            query = query.Where(x => x.WarehouseId == warehouseId.Value);

        return await query
            .OrderByDescending(x => x.TransactionDate)
            .ThenByDescending(x => x.Id)
            .Select(x => new StockMovementRowDto
            {
                TransactionDate = x.TransactionDate,
                ProductCode = x.Product!.Code,
                ProductName = x.Product!.Name,
                WarehouseName = x.Warehouse!.Name,
                TransactionType = x.TransactionType,
                QuantityIn = x.QuantityIn,
                QuantityOut = x.QuantityOut,
                ReferenceNo = x.ReferenceNo,
                Remarks = x.Remarks
            })
            .ToListAsync();
    }

    public async Task<Result> CreateAdjustmentAsync(int productId, int warehouseId, decimal quantityChange, string reason, int? actorUserId = null)
    {
        var auth = _authorizationService.EnsureWarehousePostAccess();
        if (!auth.IsSuccess)
            return Result.Failure(auth.Message);

        if (quantityChange == 0)
            return Result.Failure("Adjustment quantity cannot be zero.");
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("Adjustment reason is required.");

        var product = await _db.Products.FirstOrDefaultAsync(x => x.Id == productId && x.IsActive);
        if (product is null)
            return Result.Failure("Product not found.");

        var warehouse = await _db.Warehouses.FirstOrDefaultAsync(x => x.Id == warehouseId && x.IsActive);
        if (warehouse is null)
            return Result.Failure("Warehouse not found.");

        var stock = await _db.StockBalances.FirstOrDefaultAsync(x => x.ProductId == productId && x.WarehouseId == warehouseId);
        if (stock is null)
        {
            stock = new StockBalance { ProductId = productId, WarehouseId = warehouseId };
            _db.StockBalances.Add(stock);
        }

        var newQty = stock.QuantityOnHand + quantityChange;
        if (newQty < 0)
            return Result.Failure("Adjustment would create negative stock.");

        await using var transaction = await _db.Database.BeginTransactionAsync();

        stock.QuantityOnHand = newQty;
        var referenceNo = $"ADJ-{DateTime.Now:yyyyMMdd-HHmmssfff}";
        _db.WarehouseTransactions.Add(new WarehouseTransaction
        {
            ProductId = productId,
            WarehouseId = warehouseId,
            TransactionType = quantityChange >= 0 ? "ADJ-IN" : "ADJ-OUT",
            QuantityIn = quantityChange >= 0 ? quantityChange : 0,
            QuantityOut = quantityChange < 0 ? Math.Abs(quantityChange) : 0,
            ReferenceNo = referenceNo,
            Remarks = reason.Trim()
        });

        await _db.SaveChangesAsync();
        await _auditService.LogAsync(GetActorUserId(actorUserId), "Create", "WarehouseAdjustment", referenceNo, null, $"{productId}|{warehouseId}|{quantityChange}|{reason.Trim()}");
        await transaction.CommitAsync();
        return Result.Success("Stock adjustment saved.");
    }

    private int? GetActorUserId(int? actorUserId) => actorUserId ?? _currentUserService.CurrentUserId;
}
