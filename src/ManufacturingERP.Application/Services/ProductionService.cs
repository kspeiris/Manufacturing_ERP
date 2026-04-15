using ManufacturingERP.Application.Abstractions;
using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace ManufacturingERP.Application.Services;

public class ProductionService
{
    private readonly IAppDbContext _db;

    public ProductionService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<int>> CreateProductionOrderAsync(CreateProductionOrderRequest request)
    {
        if (request.PlannedQuantity <= 0) return Result<int>.Failure("Planned quantity must be greater than zero.");
        var product = await _db.Products.Include(x => x.ProductCategory).FirstOrDefaultAsync(x => x.Id == request.FinishedProductId && x.IsActive);
        if (product is null) return Result<int>.Failure("Finished product not found.");
        if (!string.Equals(product.ProductCategory?.Name, "Finished Goods", StringComparison.OrdinalIgnoreCase)) return Result<int>.Failure("Production orders can only be created for finished goods.");
        var entity = new ProductionOrder { OrderNo = $"PROD-{DateTime.Now:yyyyMMdd-HHmmssfff}", OrderDate = DateTime.Now, FinishedProductId = request.FinishedProductId, PlannedQuantity = request.PlannedQuantity, Status = "Planned" };
        _db.ProductionOrders.Add(entity);
        await _db.SaveChangesAsync();
        return Result<int>.Success(entity.Id, entity.OrderNo);
    }

    public async Task<Result<int>> SaveBomAsync(SaveBomRequest request)
    {
        if (request.Lines.Count == 0) return Result<int>.Failure("BOM requires at least one material line.");
        var product = await _db.Products.Include(x => x.ProductCategory).FirstOrDefaultAsync(x => x.Id == request.FinishedProductId && x.IsActive);
        if (product is null) return Result<int>.Failure("Finished product not found.");
        if (!string.Equals(product.ProductCategory?.Name, "Finished Goods", StringComparison.OrdinalIgnoreCase)) return Result<int>.Failure("BOM can only be created for finished goods.");
        if (request.Lines.Any(x => x.MaterialProductId <= 0 || x.QuantityRequired <= 0)) return Result<int>.Failure("BOM quantities must be greater than zero.");
        if (request.Lines.GroupBy(x => x.MaterialProductId).Any(x => x.Count() > 1)) return Result<int>.Failure("BOM contains duplicate material lines.");
        var materialIds = request.Lines.Select(x => x.MaterialProductId).Distinct().ToList();
        var materials = await _db.Products.Where(x => materialIds.Contains(x.Id) && x.IsActive).ToListAsync();
        if (materials.Count != materialIds.Count) return Result<int>.Failure("One or more BOM materials were not found.");
        if (materials.Any(x => x.Id == request.FinishedProductId)) return Result<int>.Failure("Finished product cannot be a BOM material for itself.");
        var existing = await _db.BomHeaders.Include(x => x.Lines).FirstOrDefaultAsync(x => x.ProductId == request.FinishedProductId);
        if (existing is null)
        {
            existing = new BomHeader { ProductId = request.FinishedProductId, Version = string.IsNullOrWhiteSpace(request.Version) ? "V1" : request.Version.Trim() };
            _db.BomHeaders.Add(existing);
        }
        else
        {
            existing.Version = string.IsNullOrWhiteSpace(request.Version) ? existing.Version : request.Version.Trim();
            _db.BomLines.RemoveRange(existing.Lines);
            existing.Lines.Clear();
        }
        foreach (var line in request.Lines) existing.Lines.Add(new BomLine { MaterialProductId = line.MaterialProductId, QuantityRequired = line.QuantityRequired });
        await _db.SaveChangesAsync();
        return Result<int>.Success(existing.Id, "BOM saved.");
    }

    public Task<BomHeader?> GetBomAsync(int finishedProductId)
        => _db.BomHeaders.Include(x => x.Product).Include(x => x.Lines).ThenInclude(x => x.MaterialProduct).FirstOrDefaultAsync(x => x.ProductId == finishedProductId);

    public async Task<Result> IssueMaterialsAsync(int productionOrderId, int warehouseId, decimal basisQuantity)
    {
        if (basisQuantity <= 0) return Result.Failure("Issue quantity must be greater than zero.");
        var order = await _db.ProductionOrders.Include(x => x.FinishedProduct).FirstOrDefaultAsync(x => x.Id == productionOrderId);
        if (order is null) return Result.Failure("Production order not found.");
        var warehouse = await _db.Warehouses.FirstOrDefaultAsync(x => x.Id == warehouseId && x.IsActive);
        if (warehouse is null) return Result.Failure("Warehouse not found.");
        var bom = await _db.BomHeaders.Include(x => x.Lines).ThenInclude(x => x.MaterialProduct).FirstOrDefaultAsync(x => x.ProductId == order.FinishedProductId);
        if (bom is null || bom.Lines.Count == 0) return Result.Failure("No BOM found for the finished product.");
        if (order.ProducedQuantity + order.ScrapQuantity + basisQuantity > order.PlannedQuantity) return Result.Failure("Material issue exceeds the planned production quantity.");
        foreach (var bomLine in bom.Lines)
        {
            var requiredQty = bomLine.QuantityRequired * basisQuantity;
            var stock = await _db.StockBalances.FirstOrDefaultAsync(x => x.ProductId == bomLine.MaterialProductId && x.WarehouseId == warehouseId);
            if (stock is null || stock.QuantityOnHand < requiredQty) return Result.Failure($"Insufficient stock for material {bomLine.MaterialProduct?.Code ?? bomLine.MaterialProductId.ToString()}.");
        }
        decimal materialCost = 0;
        foreach (var bomLine in bom.Lines)
        {
            var requiredQty = bomLine.QuantityRequired * basisQuantity;
            var stock = await _db.StockBalances.FirstAsync(x => x.ProductId == bomLine.MaterialProductId && x.WarehouseId == warehouseId);
            stock.QuantityOnHand -= requiredQty;
            var lineCost = requiredQty * (bomLine.MaterialProduct?.CostPrice ?? 0m);
            materialCost += lineCost;
            _db.ProductionMaterialIssues.Add(new ProductionMaterialIssue { ProductionOrderId = order.Id, MaterialProductId = bomLine.MaterialProductId, QuantityIssued = requiredQty, IssueDate = DateTime.Now });
            _db.WarehouseTransactions.Add(new WarehouseTransaction { ProductId = bomLine.MaterialProductId, WarehouseId = warehouseId, TransactionType = "PROD-ISSUE", QuantityIn = 0, QuantityOut = requiredQty, ReferenceNo = order.OrderNo, Remarks = $"Production material issue for {order.FinishedProduct?.Code}" });
        }
        order.MaterialCost += materialCost;
        order.Status = order.Status == "Planned" ? "Materials Issued" : "In Progress";
        await _db.SaveChangesAsync();
        return Result.Success("Production materials issued.");
    }

    public async Task<Result> ReceiveFinishedGoodsAsync(ReceiveFinishedGoodsRequest request)
    {
        if (request.ProducedQuantity <= 0) return Result.Failure("Produced quantity must be greater than zero.");
        if (request.ScrapQuantity < 0) return Result.Failure("Scrap quantity cannot be negative.");
        var order = await _db.ProductionOrders.Include(x => x.FinishedProduct).FirstOrDefaultAsync(x => x.Id == request.ProductionOrderId);
        if (order is null) return Result.Failure("Production order not found.");
        var warehouse = await _db.Warehouses.FirstOrDefaultAsync(x => x.Id == request.WarehouseId && x.IsActive);
        if (warehouse is null) return Result.Failure("Warehouse not found.");
        var bom = await _db.BomHeaders.Include(x => x.Lines).FirstOrDefaultAsync(x => x.ProductId == order.FinishedProductId);
        if (bom is null || bom.Lines.Count == 0) return Result.Failure("No BOM found for the finished product.");
        var issues = await _db.ProductionMaterialIssues.Where(x => x.ProductionOrderId == order.Id).Select(x => new { x.MaterialProductId, x.QuantityIssued }).ToListAsync();
        if (issues.Count == 0) return Result.Failure("Issue raw materials before receiving finished goods.");
        var issuedEquivalent = bom.Lines.Min(line => line.QuantityRequired == 0 ? decimal.MaxValue : issues.Where(x => x.MaterialProductId == line.MaterialProductId).Sum(x => x.QuantityIssued) / line.QuantityRequired);
        var newProcessedTotal = order.ProducedQuantity + order.ScrapQuantity + request.ProducedQuantity + request.ScrapQuantity;
        if (newProcessedTotal > order.PlannedQuantity) return Result.Failure("Finished goods receipt exceeds the planned quantity.");
        if (newProcessedTotal > issuedEquivalent) return Result.Failure("Finished goods receipt exceeds issued raw material coverage.");
        order.ProducedQuantity += request.ProducedQuantity;
        order.ScrapQuantity += request.ScrapQuantity;
        if (!string.IsNullOrWhiteSpace(request.BatchNo)) order.BatchNo = request.BatchNo.Trim();
        else if (string.IsNullOrWhiteSpace(order.BatchNo)) order.BatchNo = $"BATCH-{order.OrderNo}";
        order.Status = order.ProducedQuantity + order.ScrapQuantity >= order.PlannedQuantity ? "Completed" : "In Progress";
        var stock = await _db.StockBalances.FirstOrDefaultAsync(x => x.ProductId == order.FinishedProductId && x.WarehouseId == request.WarehouseId);
        if (stock is null)
        {
            stock = new StockBalance { ProductId = order.FinishedProductId, WarehouseId = request.WarehouseId };
            _db.StockBalances.Add(stock);
        }
        stock.QuantityOnHand += request.ProducedQuantity;
        _db.WarehouseTransactions.Add(new WarehouseTransaction { ProductId = order.FinishedProductId, WarehouseId = request.WarehouseId, TransactionType = "PROD-RECEIPT", QuantityIn = request.ProducedQuantity, QuantityOut = 0, ReferenceNo = order.OrderNo, Remarks = string.IsNullOrWhiteSpace(order.BatchNo) ? "Finished goods receipt" : $"Finished goods receipt / {order.BatchNo}" });
        await _db.SaveChangesAsync();
        return Result.Success("Finished goods received.");
    }

    public async Task<Result> SaveCostingAsync(int productionOrderId, decimal laborCost, decimal overheadCost)
    {
        if (laborCost < 0 || overheadCost < 0) return Result.Failure("Labor and overhead costs cannot be negative.");
        var order = await _db.ProductionOrders.FirstOrDefaultAsync(x => x.Id == productionOrderId);
        if (order is null) return Result.Failure("Production order not found.");
        var issuedLines = await _db.ProductionMaterialIssues.Where(x => x.ProductionOrderId == productionOrderId).Join(_db.Products, issue => issue.MaterialProductId, product => product.Id, (issue, product) => new { issue.QuantityIssued, product.CostPrice }).ToListAsync();
        order.MaterialCost = issuedLines.Sum(x => x.QuantityIssued * x.CostPrice);
        order.LaborCost = laborCost;
        order.OverheadCost = overheadCost;
        await _db.SaveChangesAsync();
        return Result.Success("Production costing updated.");
    }

    public async Task<List<ProductionLedgerRowDto>> GetProductionLedgerAsync(int productionOrderId)
    {
        var order = await _db.ProductionOrders.Include(x => x.FinishedProduct).FirstOrDefaultAsync(x => x.Id == productionOrderId);
        if (order is null) return new List<ProductionLedgerRowDto>();
        var issueRows = await _db.ProductionMaterialIssues.Where(x => x.ProductionOrderId == productionOrderId).Include(x => x.MaterialProduct).OrderBy(x => x.IssueDate).ThenBy(x => x.Id).Select(x => new ProductionLedgerRowDto { EntryDate = x.IssueDate, EntryType = "Material Issue", ProductCode = x.MaterialProduct!.Code, ProductName = x.MaterialProduct!.Name, Quantity = x.QuantityIssued, Amount = x.QuantityIssued * x.MaterialProduct!.CostPrice, ReferenceNo = order.OrderNo, Remarks = "Raw material consumed" }).ToListAsync();
        var receiptRows = await _db.WarehouseTransactions.Where(x => x.ReferenceNo == order.OrderNo && x.TransactionType == "PROD-RECEIPT").Include(x => x.Product).OrderBy(x => x.TransactionDate).ThenBy(x => x.Id).Select(x => new ProductionLedgerRowDto { EntryDate = x.TransactionDate, EntryType = "Finished Goods Receipt", ProductCode = x.Product!.Code, ProductName = x.Product!.Name, Quantity = x.QuantityIn, Amount = 0, ReferenceNo = x.ReferenceNo, Remarks = x.Remarks }).ToListAsync();
        var costingRow = new ProductionLedgerRowDto { EntryDate = order.OrderDate, EntryType = "Cost Summary", ProductCode = order.FinishedProduct?.Code ?? string.Empty, ProductName = order.FinishedProduct?.Name ?? string.Empty, Quantity = order.ProducedQuantity, Amount = order.TotalCost, ReferenceNo = order.OrderNo, Remarks = $"Unit cost {order.UnitCost:N2}" };
        return issueRows.Concat(receiptRows).Append(costingRow).OrderBy(x => x.EntryDate).ThenBy(x => x.EntryType).ToList();
    }
}
