using ManufacturingERP.Application.Abstractions;
using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace ManufacturingERP.Application.Services;

public class ProcurementService
{
    private readonly IAppDbContext _db;
    private readonly AuthorizationService _authorizationService;
    private readonly AuditService _auditService;
    private readonly CurrentUserService _currentUserService;

    public ProcurementService(IAppDbContext db, AuthorizationService authorizationService, AuditService auditService, CurrentUserService currentUserService)
    {
        _db = db;
        _authorizationService = authorizationService;
        _auditService = auditService;
        _currentUserService = currentUserService;
    }

    public async Task<Result<int>> CreatePurchaseOrderAsync(CreatePurchaseOrderRequest request, int? actorUserId = null)
    {
        var auth = _authorizationService.EnsureProcurementPostAccess();
        if (!auth.IsSuccess)
            return Result<int>.Failure(auth.Message);

        if (!request.Items.Any())
            return Result<int>.Failure("Purchase order requires at least one line.");

        var supplier = await _db.Suppliers.FirstOrDefaultAsync(x => x.Id == request.SupplierId);
        if (supplier is null)
            return Result<int>.Failure("Supplier not found.");

        var invalidLine = request.Items.FirstOrDefault(x => x.ProductId <= 0 || x.Quantity <= 0 || x.UnitPrice <= 0);
        if (invalidLine is not null)
            return Result<int>.Failure("Purchase order quantities and unit prices must be greater than zero.");

        var productIds = request.Items.Select(x => x.ProductId).Distinct().ToList();
        var validProductIds = await _db.Products.Where(x => productIds.Contains(x.Id) && x.IsActive).Select(x => x.Id).ToListAsync();
        var missingProductId = productIds.Except(validProductIds).FirstOrDefault();
        if (missingProductId != 0)
            return Result<int>.Failure($"Product ID {missingProductId} was not found or is inactive.");

        var po = new PurchaseOrder
        {
            OrderNo = $"PO-{DateTime.Now:yyyyMMdd-HHmmssfff}",
            OrderDate = DateTime.Now,
            SupplierId = request.SupplierId,
            Notes = request.Notes,
            Status = "Pending Approval",
            Items = request.Items.Select(x => new PurchaseOrderItem
            {
                ProductId = x.ProductId,
                Quantity = x.Quantity,
                UnitPrice = x.UnitPrice
            }).ToList()
        };

        po.TotalAmount = po.Items.Sum(x => x.LineTotal);
        await using var transaction = await _db.Database.BeginTransactionAsync();
        _db.PurchaseOrders.Add(po);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(GetActorUserId(actorUserId), "Create", "PurchaseOrder", po.Id.ToString(), null, po.OrderNo);
        await transaction.CommitAsync();
        return Result<int>.Success(po.Id, po.OrderNo);
    }

    public async Task<Result<int>> CreateGoodsReceiptAsync(CreateGoodsReceiptRequest request, int? actorUserId = null)
    {
        var auth = _authorizationService.EnsureProcurementPostAccess();
        if (!auth.IsSuccess)
            return Result<int>.Failure(auth.Message);

        if (!request.Items.Any())
            return Result<int>.Failure("Goods receipt requires at least one line.");

        var supplier = await _db.Suppliers.FirstOrDefaultAsync(x => x.Id == request.SupplierId);
        if (supplier is null)
            return Result<int>.Failure("Supplier not found.");

        var warehouse = await _db.Warehouses.FirstOrDefaultAsync(x => x.Id == request.WarehouseId && x.IsActive);
        if (warehouse is null)
            return Result<int>.Failure("Warehouse not found or inactive.");

        var invalidLine = request.Items.FirstOrDefault(x => x.ProductId <= 0 || x.Quantity <= 0 || x.UnitCost <= 0);
        if (invalidLine is not null)
            return Result<int>.Failure("Goods receipt quantities and unit costs must be greater than zero.");

        var productIds = request.Items.Select(x => x.ProductId).Distinct().ToList();
        var validProductIds = await _db.Products.Where(x => productIds.Contains(x.Id) && x.IsActive).Select(x => x.Id).ToListAsync();
        var missingProductId = productIds.Except(validProductIds).FirstOrDefault();
        if (missingProductId != 0)
            return Result<int>.Failure($"Product ID {missingProductId} was not found or is inactive.");

        PurchaseOrder? purchaseOrder = null;
        if (!string.IsNullOrWhiteSpace(request.PurchaseOrderNo))
        {
            purchaseOrder = await _db.PurchaseOrders
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x => x.OrderNo == request.PurchaseOrderNo);

            if (purchaseOrder is null)
                return Result<int>.Failure("Referenced purchase order was not found.");

            if (purchaseOrder.SupplierId != request.SupplierId)
                return Result<int>.Failure("Purchase order supplier does not match the goods receipt supplier.");

            if (string.Equals(purchaseOrder.Status, "Closed", StringComparison.OrdinalIgnoreCase))
                return Result<int>.Failure("Purchase order is already fully received.");

            var receivedTotals = await _db.GoodsReceiptItems
                .Where(x => x.GoodsReceipt!.PurchaseOrderNo == request.PurchaseOrderNo)
                .Select(x => new { x.ProductId, x.Quantity })
                .ToListAsync();

            var receivedTotalsByProduct = receivedTotals
                .GroupBy(x => x.ProductId)
                .ToDictionary(x => x.Key, x => x.Sum(y => y.Quantity));

            foreach (var line in request.Items)
            {
                var poLine = purchaseOrder.Items.FirstOrDefault(x => x.ProductId == line.ProductId);
                if (poLine is null)
                    return Result<int>.Failure($"Product ID {line.ProductId} is not on purchase order {purchaseOrder.OrderNo}.");

                receivedTotalsByProduct.TryGetValue(line.ProductId, out var alreadyReceived);
                if (alreadyReceived + line.Quantity > poLine.Quantity)
                    return Result<int>.Failure($"Goods receipt quantity exceeds ordered quantity for product ID {line.ProductId}.");
            }
        }

        var receipt = new GoodsReceipt
        {
            ReceiptNo = $"GRN-{DateTime.Now:yyyyMMdd-HHmmssfff}",
            ReceiptDate = DateTime.Now,
            SupplierId = request.SupplierId,
            WarehouseId = request.WarehouseId,
            PurchaseOrderNo = request.PurchaseOrderNo,
            Notes = request.Notes,
            Items = request.Items.Select(x => new GoodsReceiptItem
            {
                ProductId = x.ProductId,
                Quantity = x.Quantity,
                UnitCost = x.UnitCost
            }).ToList()
        };
        receipt.TotalAmount = receipt.Items.Sum(x => x.LineTotal);

        await using var transaction = await _db.Database.BeginTransactionAsync();

        foreach (var line in request.Items)
        {
            var stock = await _db.StockBalances.FirstOrDefaultAsync(x => x.ProductId == line.ProductId && x.WarehouseId == request.WarehouseId);
            if (stock is null)
            {
                stock = new StockBalance
                {
                    ProductId = line.ProductId,
                    WarehouseId = request.WarehouseId,
                    QuantityOnHand = 0
                };
                _db.StockBalances.Add(stock);
            }
            stock.QuantityOnHand += line.Quantity;

            _db.WarehouseTransactions.Add(new WarehouseTransaction
            {
                ProductId = line.ProductId,
                WarehouseId = request.WarehouseId,
                TransactionType = "GRN",
                QuantityIn = line.Quantity,
                QuantityOut = 0,
                ReferenceNo = receipt.ReceiptNo,
                Remarks = "Goods receipt from supplier"
            });
        }

        _db.GoodsReceipts.Add(receipt);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(GetActorUserId(actorUserId), "Create", "GoodsReceipt", receipt.Id.ToString(), null, receipt.ReceiptNo);

        if (purchaseOrder is not null)
        {
            var receivedAfterSave = await _db.GoodsReceiptItems
                .Where(x => x.GoodsReceipt!.PurchaseOrderNo == purchaseOrder.OrderNo)
                .Select(x => new { x.ProductId, x.Quantity })
                .ToListAsync();

            var receivedAfterSaveByProduct = receivedAfterSave
                .GroupBy(x => x.ProductId)
                .ToDictionary(x => x.Key, x => x.Sum(y => y.Quantity));

            var isFullyReceived = purchaseOrder.Items.All(x => receivedAfterSaveByProduct.TryGetValue(x.ProductId, out var qty) && qty >= x.Quantity);
            purchaseOrder.Status = isFullyReceived ? "Closed" : "Partially Received";
            await _db.SaveChangesAsync();
        }

        await transaction.CommitAsync();
        return Result<int>.Success(receipt.Id, receipt.ReceiptNo);
    }

    private int? GetActorUserId(int? actorUserId) => actorUserId ?? _currentUserService.CurrentUserId;
}
