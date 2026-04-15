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

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
                return Result<int>.Failure("Vehicle load quantities must be greater than zero.");

            var stock = await _db.StockBalances.FirstOrDefaultAsync(x => x.ProductId == item.ProductId && x.WarehouseId == warehouseId);
            if (stock is null || stock.QuantityOnHand < item.Quantity)
                return Result<int>.Failure($"Insufficient warehouse stock for product ID {item.ProductId}.");

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
        return Result<int>.Success(vehicleLoad.Id, "Vehicle load created.");
    }
}
