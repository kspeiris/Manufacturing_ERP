using ManufacturingERP.Application.Abstractions;
using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace ManufacturingERP.Application.Services;

public class InventoryService
{
    private readonly IAppDbContext _db;

    public InventoryService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<List<BatchLotSelectionDto>>> SelectLotsAsync(int productId, int warehouseId, decimal quantity, LotSelectionMethod method = LotSelectionMethod.Fifo)
    {
        if (quantity <= 0)
            return Result<List<BatchLotSelectionDto>>.Failure("Quantity must be greater than zero.");

        var product = await _db.Products.FirstOrDefaultAsync(x => x.Id == productId && x.IsActive);
        if (product is null)
            return Result<List<BatchLotSelectionDto>>.Failure("Product not found.");

        if (!product.TrackBatch)
            return Result<List<BatchLotSelectionDto>>.Success([], "Product is not batch tracked.");

        var lots = await _db.BatchLots
            .Where(x => x.ProductId == productId && x.WarehouseId == warehouseId && x.IsActive && x.QuantityOnHand > x.QuantityReserved)
            .OrderBy(x => method == LotSelectionMethod.Fifo ? x.ManufacturingDate : x.CreatedAtUtc)
            .ThenBy(x => x.ExpiryDate)
            .ToListAsync();

        var available = lots.Sum(x => x.QuantityAvailable);
        if (available < quantity)
            return Result<List<BatchLotSelectionDto>>.Failure($"Insufficient batch stock. Available: {available}.");

        var averageCost = method == LotSelectionMethod.WeightedAverage && available > 0
            ? product.CostPrice
            : product.CostPrice;

        var remaining = quantity;
        var selection = new List<BatchLotSelectionDto>();
        foreach (var lot in lots)
        {
            if (remaining <= 0) break;
            var selected = Math.Min(lot.QuantityAvailable, remaining);
            selection.Add(new BatchLotSelectionDto
            {
                BatchLotId = lot.Id,
                LotNumber = lot.LotNumber,
                ExpiryDate = lot.ExpiryDate,
                QuantitySelected = selected,
                UnitCost = averageCost
            });
            remaining -= selected;
        }

        return Result<List<BatchLotSelectionDto>>.Success(selection);
    }

    public async Task<Result> ReserveStockAsync(StockReservationRequest request)
    {
        if (request.Quantity <= 0)
            return Result.Failure("Reservation quantity must be greater than zero.");

        var product = await _db.Products.FirstOrDefaultAsync(x => x.Id == request.ProductId && x.IsActive);
        if (product is null)
            return Result.Failure("Product not found.");

        var stock = await _db.StockBalances.FirstOrDefaultAsync(x => x.ProductId == request.ProductId && x.WarehouseId == request.WarehouseId);
        if (stock is null || stock.QuantityAvailable < request.Quantity)
            return Result.Failure($"Insufficient available stock for product ID {request.ProductId}.");

        await using var transaction = await _db.Database.BeginTransactionAsync();

        if (product.TrackBatch)
        {
            var lotSelection = await SelectLotsAsync(request.ProductId, request.WarehouseId, request.Quantity, request.SelectionMethod);
            if (!lotSelection.IsSuccess || lotSelection.Value is null)
                return Result.Failure(lotSelection.Message);

            var lotIds = lotSelection.Value.Select(x => x.BatchLotId).ToList();
            var lots = await _db.BatchLots.Where(x => lotIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
            foreach (var selected in lotSelection.Value)
                lots[selected.BatchLotId].Reserve(selected.QuantitySelected);
        }

        stock.QuantityReserved += request.Quantity;
        _db.WarehouseTransactions.Add(new WarehouseTransaction
        {
            ProductId = request.ProductId,
            WarehouseId = request.WarehouseId,
            TransactionType = "RESERVE",
            ReferenceNo = $"RSV-{DateTime.Now:yyyyMMdd-HHmmssfff}",
            Remarks = "Stock reserved",
            QuantityIn = 0,
            QuantityOut = 0
        });

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Result.Success("Stock reserved.");
    }

    public async Task<List<ReorderAlertDto>> GetReorderAlertsAsync()
    {
        return await _db.StockBalances
            .Include(x => x.Product)
            .Include(x => x.Warehouse)
            .Where(x => x.Product != null && x.Product.ReorderLevel > 0 && x.QuantityOnHand - x.QuantityReserved <= x.Product.ReorderLevel)
            .OrderBy(x => x.Product!.Code)
            .Select(x => new ReorderAlertDto
            {
                ProductId = x.ProductId,
                ProductCode = x.Product!.Code,
                ProductName = x.Product.Name,
                WarehouseName = x.Warehouse!.Name,
                QuantityOnHand = x.QuantityOnHand,
                QuantityReserved = x.QuantityReserved,
                QuantityAvailable = x.QuantityOnHand - x.QuantityReserved,
                ReorderLevel = x.Product.ReorderLevel
            })
            .ToListAsync();
    }

    public async Task<Result<int>> CreateStockCountAsync(CreateStockCountRequest request)
    {
        if (request.InitiatedByUserId <= 0)
            return Result<int>.Failure("Stock count requires an initiating user.");
        if (!request.Lines.Any())
            return Result<int>.Failure("Stock count requires at least one line.");

        var warehouse = await _db.Warehouses.FirstOrDefaultAsync(x => x.Id == request.WarehouseId && x.IsActive);
        if (warehouse is null)
            return Result<int>.Failure("Warehouse not found.");

        var productIds = request.Lines.Select(x => x.ProductId).Distinct().ToList();
        var validProductCount = await _db.Products.CountAsync(x => productIds.Contains(x.Id) && x.IsActive);
        if (validProductCount != productIds.Count)
            return Result<int>.Failure("One or more stock count products are invalid.");

        var count = new StockCount
        {
            CountNo = $"SC-{DateTime.Now:yyyyMMdd-HHmmssfff}",
            CountDate = request.CountDate.Date,
            WarehouseId = request.WarehouseId,
            WarehouseBinId = request.WarehouseBinId,
            InitiatedByUserId = request.InitiatedByUserId,
            Notes = request.Notes,
            Lines = request.Lines.Select(x => new StockCountLine
            {
                ProductId = x.ProductId,
                BatchLotId = x.BatchLotId,
                BookQuantity = x.BookQuantity,
                CountedQuantity = x.CountedQuantity,
                UnitCost = x.UnitCost,
                Notes = x.Notes
            }).ToList()
        };

        _db.StockCounts.Add(count);
        await _db.SaveChangesAsync();
        return Result<int>.Success(count.Id, count.CountNo);
    }

    public async Task<Result> StartStockCountAsync(int stockCountId)
    {
        var count = await _db.StockCounts.FirstOrDefaultAsync(x => x.Id == stockCountId);
        if (count is null) return Result.Failure("Stock count not found.");

        try
        {
            count.Start();
            await _db.SaveChangesAsync();
            return Result.Success("Stock count started.");
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public async Task<Result> SubmitStockCountAsync(int stockCountId)
    {
        var count = await _db.StockCounts.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == stockCountId);
        if (count is null) return Result.Failure("Stock count not found.");

        try
        {
            count.Submit();
            await _db.SaveChangesAsync();
            return Result.Success("Stock count submitted for approval.");
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public async Task<Result> ApproveStockCountAsync(int stockCountId, int approvedByUserId)
    {
        if (approvedByUserId <= 0)
            return Result.Failure("Stock count approval requires a user.");

        var count = await _db.StockCounts.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == stockCountId);
        if (count is null) return Result.Failure("Stock count not found.");

        await using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            count.Approve(approvedByUserId);
            foreach (var line in count.Lines.Where(x => x.HasVariance))
            {
                var stock = await _db.StockBalances.FirstOrDefaultAsync(x => x.ProductId == line.ProductId && x.WarehouseId == count.WarehouseId);
                if (stock is null)
                {
                    stock = new StockBalance { ProductId = line.ProductId, WarehouseId = count.WarehouseId };
                    _db.StockBalances.Add(stock);
                }

                var delta = line.VarianceQuantity;
                if (stock.QuantityOnHand + delta < 0)
                    return Result.Failure($"Stock count adjustment would make product ID {line.ProductId} negative.");

                stock.QuantityOnHand += delta;

                if (line.BatchLotId.HasValue)
                {
                    var lot = await _db.BatchLots.FirstOrDefaultAsync(x => x.Id == line.BatchLotId.Value);
                    if (lot is null) return Result.Failure($"Batch lot not found for product ID {line.ProductId}.");
                    if (lot.QuantityOnHand + delta < 0)
                        return Result.Failure($"Stock count adjustment would make lot {lot.LotNumber} negative.");

                    lot.QuantityOnHand += delta;
                    lot.UpdatedAtUtc = DateTime.UtcNow;
                }

                _db.WarehouseTransactions.Add(new WarehouseTransaction
                {
                    ProductId = line.ProductId,
                    WarehouseId = count.WarehouseId,
                    TransactionDate = count.CountDate,
                    TransactionType = delta > 0 ? "ADJ-IN" : "ADJ-OUT",
                    QuantityIn = delta > 0 ? delta : 0,
                    QuantityOut = delta < 0 ? Math.Abs(delta) : 0,
                    ReferenceNo = count.CountNo,
                    Remarks = "Physical stock count adjustment"
                });
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return Result.Success("Stock count approved and inventory adjusted.");
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public async Task<Result<int>> CreateVehicleLoadAsync(LoadVehicleRequest request, int warehouseId = 1)
    {
        if (!request.Items.Any())
            return Result<int>.Failure("At least one item is required.");

        var vehicle = await _db.Vehicles.FirstOrDefaultAsync(x => x.Id == request.VehicleId && x.IsActive);
        if (vehicle is null)
            return Result<int>.Failure("Vehicle not found.");

        var warehouse = await _db.Warehouses.FirstOrDefaultAsync(x => x.Id == warehouseId && x.IsActive);
        if (warehouse is null)
            return Result<int>.Failure("Warehouse not found.");

        var vehicleLoad = new VehicleLoad
        {
            VehicleId = request.VehicleId,
            RouteName = request.RouteName,
            LoadDate = request.LoadDate,
            Items = request.Items.Select(i => new VehicleLoadItem
            {
                ProductId = i.ProductId,
                QuantityLoaded = i.Quantity
            }).ToList()
        };

        await using var transaction = await _db.Database.BeginTransactionAsync();

        foreach (var group in request.Items.GroupBy(x => x.ProductId))
        {
            var requestedQuantity = group.Sum(x => x.Quantity);
            if (requestedQuantity <= 0)
                return Result<int>.Failure("Vehicle load quantities must be greater than zero.");

            var stock = await _db.StockBalances.FirstOrDefaultAsync(x => x.ProductId == group.Key && x.WarehouseId == warehouseId);
            if (stock is null || stock.QuantityAvailable < requestedQuantity)
                return Result<int>.Failure($"Insufficient available warehouse stock for product ID {group.Key}.");
        }

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
                return Result<int>.Failure("Vehicle load quantities must be greater than zero.");

            var stock = await _db.StockBalances.FirstOrDefaultAsync(x => x.ProductId == item.ProductId && x.WarehouseId == warehouseId);
            if (stock is null)
                return Result<int>.Failure($"Insufficient available warehouse stock for product ID {item.ProductId}.");

            var product = await _db.Products.FirstAsync(x => x.Id == item.ProductId);
            if (product.TrackBatch)
            {
                var lotSelection = await SelectLotsAsync(item.ProductId, warehouseId, item.Quantity);
                if (!lotSelection.IsSuccess || lotSelection.Value is null)
                    return Result<int>.Failure(lotSelection.Message);

                var lotIds = lotSelection.Value.Select(x => x.BatchLotId).ToList();
                var lots = await _db.BatchLots.Where(x => lotIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
                foreach (var selected in lotSelection.Value)
                    lots[selected.BatchLotId].Consume(selected.QuantitySelected);
            }

            stock.QuantityOnHand -= item.Quantity;

            _db.WarehouseTransactions.Add(new WarehouseTransaction
            {
                ProductId = item.ProductId,
                WarehouseId = warehouseId,
                TransactionType = "VEH-LOAD",
                QuantityIn = 0,
                QuantityOut = item.Quantity,
                ReferenceNo = $"VLOAD-{request.LoadDate:yyyyMMdd}-{request.VehicleId}",
                Remarks = string.IsNullOrWhiteSpace(request.RouteName)
                    ? $"Vehicle load to {vehicle.VehicleNumber}"
                    : $"Vehicle load to {vehicle.VehicleNumber} / {request.RouteName}"
            });
        }

        _db.VehicleLoads.Add(vehicleLoad);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Result<int>.Success(vehicleLoad.Id, "Vehicle load created.");
    }
}
