using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Application.Services;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ManufacturingERP.Tests;

public class ProductionWorkflowTests
{
    [Fact]
    public async Task ProductionWorkflow_Should_IssueMaterials_ReceiveFinishedGoods_And_UpdateCosting()
    {
        await using var harness = await ProductionTestHarness.CreateAsync();

        var bom = await harness.ProductionService.SaveBomAsync(new SaveBomRequest
        {
            FinishedProductId = harness.FinishedProduct.Id,
            Version = "V2",
            Lines =
            [
                new SaveBomLineRequest { MaterialProductId = harness.RawMaterialA.Id, QuantityRequired = 2.5m },
                new SaveBomLineRequest { MaterialProductId = harness.RawMaterialB.Id, QuantityRequired = 1m }
            ]
        });
        Assert.True(bom.IsSuccess, bom.Message);

        var order = await harness.ProductionService.CreateProductionOrderAsync(new CreateProductionOrderRequest
        {
            FinishedProductId = harness.FinishedProduct.Id,
            PlannedQuantity = 10m
        });
        Assert.True(order.IsSuccess, order.Message);

        var productionOrderId = await harness.Db.ProductionOrders.OrderByDescending(x => x.Id).Select(x => x.Id).FirstAsync();
        var issue = await harness.ProductionService.IssueMaterialsAsync(productionOrderId, harness.Warehouse.Id, 10m);
        Assert.True(issue.IsSuccess, issue.Message);

        var receipt = await harness.ProductionService.ReceiveFinishedGoodsAsync(new ReceiveFinishedGoodsRequest
        {
            ProductionOrderId = productionOrderId,
            WarehouseId = harness.Warehouse.Id,
            ProducedQuantity = 9m,
            ScrapQuantity = 1m,
            BatchNo = "BATCH-001"
        });
        Assert.True(receipt.IsSuccess, receipt.Message);

        var costing = await harness.ProductionService.SaveCostingAsync(productionOrderId, 90m, 45m);
        Assert.True(costing.IsSuccess, costing.Message);

        await harness.ReloadAsync();
        Assert.Equal(75m, harness.RawStockA.QuantityOnHand);
        Assert.Equal(90m, harness.RawStockB.QuantityOnHand);
        Assert.Equal(9m, harness.FgStock.QuantityOnHand);
        Assert.Equal(9m, harness.Order.ProducedQuantity);
        Assert.Equal(1m, harness.Order.ScrapQuantity);
        Assert.Equal("BATCH-001", harness.Order.BatchNo);
        Assert.Equal("Completed", harness.Order.Status);
        Assert.Equal(65m, harness.Order.MaterialCost);
        Assert.Equal(200m, harness.Order.TotalCost);
        Assert.Equal(200m / 9m, harness.Order.UnitCost);

        var ledger = await harness.ProductionService.GetProductionLedgerAsync(productionOrderId);
        Assert.Equal(4, ledger.Count);
        Assert.Contains(ledger, x => x.EntryType == "Material Issue" && x.Amount == 50m);
        Assert.Contains(ledger, x => x.EntryType == "Material Issue" && x.Amount == 15m);
        Assert.Contains(ledger, x => x.EntryType == "Finished Goods Receipt" && x.Quantity == 9m);

        var analytics = await harness.AnalyticsService.GetAnalyticsAsync();
        Assert.Equal(200m, analytics.ProductionCostThisMonth);
    }

    [Fact]
    public async Task FinishedGoodsReceipt_Should_RequireIssuedMaterials()
    {
        await using var harness = await ProductionTestHarness.CreateAsync();

        await harness.ProductionService.SaveBomAsync(new SaveBomRequest
        {
            FinishedProductId = harness.FinishedProduct.Id,
            Lines = [new SaveBomLineRequest { MaterialProductId = harness.RawMaterialA.Id, QuantityRequired = 1m }]
        });

        var order = await harness.ProductionService.CreateProductionOrderAsync(new CreateProductionOrderRequest { FinishedProductId = harness.FinishedProduct.Id, PlannedQuantity = 5m });
        var productionOrderId = await harness.Db.ProductionOrders.OrderByDescending(x => x.Id).Select(x => x.Id).FirstAsync();
        var result = await harness.ProductionService.ReceiveFinishedGoodsAsync(new ReceiveFinishedGoodsRequest { ProductionOrderId = productionOrderId, WarehouseId = harness.Warehouse.Id, ProducedQuantity = 1m, ScrapQuantity = 0m, BatchNo = "B-1" });

        Assert.False(result.IsSuccess);
        Assert.Contains("Issue raw materials", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ProductionTestHarness : IAsyncDisposable
    {
        private readonly string _tempDirectory;

        private ProductionTestHarness(string tempDirectory, AppDbContext db, ProductionService productionService, AnalyticsService analyticsService, Product finishedProduct, Product rawMaterialA, Product rawMaterialB, Warehouse warehouse, StockBalance fgStock, StockBalance rawStockA, StockBalance rawStockB)
        {
            _tempDirectory = tempDirectory;
            Db = db;
            ProductionService = productionService;
            AnalyticsService = analyticsService;
            FinishedProduct = finishedProduct;
            RawMaterialA = rawMaterialA;
            RawMaterialB = rawMaterialB;
            Warehouse = warehouse;
            FgStock = fgStock;
            RawStockA = rawStockA;
            RawStockB = rawStockB;
        }

        public AppDbContext Db { get; }
        public ProductionService ProductionService { get; }
        public AnalyticsService AnalyticsService { get; }
        public Product FinishedProduct { get; private set; }
        public Product RawMaterialA { get; private set; }
        public Product RawMaterialB { get; private set; }
        public Warehouse Warehouse { get; private set; }
        public StockBalance FgStock { get; private set; }
        public StockBalance RawStockA { get; private set; }
        public StockBalance RawStockB { get; private set; }
        public ProductionOrder Order { get; private set; } = null!;

        public static async Task<ProductionTestHarness> CreateAsync()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "ManufacturingERP.ProductionTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            var databasePath = Path.Combine(tempDirectory, "production-workflow.db");
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={databasePath}").Options;
            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var fgCategory = new ProductCategory { Name = "Finished Goods" };
            var rmCategory = new ProductCategory { Name = "Raw Materials" };
            var finishedProduct = new Product { Code = "FG-T01", Name = "Finished Good", ProductCategory = fgCategory, Unit = "PCS", CostPrice = 12m, SellingPrice = 20m, IsActive = true };
            var rawMaterialA = new Product { Code = "RM-T01", Name = "Raw A", ProductCategory = rmCategory, Unit = "KG", CostPrice = 2m, SellingPrice = 3m, IsActive = true };
            var rawMaterialB = new Product { Code = "RM-T02", Name = "Raw B", ProductCategory = rmCategory, Unit = "PCS", CostPrice = 1.5m, SellingPrice = 2m, IsActive = true };
            var warehouse = new Warehouse { Name = "Main Warehouse", Location = "Plant", IsActive = true };

            db.ProductCategories.AddRange(fgCategory, rmCategory);
            db.Products.AddRange(finishedProduct, rawMaterialA, rawMaterialB);
            db.Warehouses.Add(warehouse);
            await db.SaveChangesAsync();

            var fgStock = new StockBalance { ProductId = finishedProduct.Id, WarehouseId = warehouse.Id, QuantityOnHand = 0m };
            var rawStockA = new StockBalance { ProductId = rawMaterialA.Id, WarehouseId = warehouse.Id, QuantityOnHand = 100m };
            var rawStockB = new StockBalance { ProductId = rawMaterialB.Id, WarehouseId = warehouse.Id, QuantityOnHand = 100m };
            db.StockBalances.AddRange(fgStock, rawStockA, rawStockB);
            await db.SaveChangesAsync();

            return new ProductionTestHarness(tempDirectory, db, new ProductionService(db), new AnalyticsService(db), finishedProduct, rawMaterialA, rawMaterialB, warehouse, fgStock, rawStockA, rawStockB);
        }

        public async Task ReloadAsync()
        {
            FinishedProduct = await Db.Products.FirstAsync(x => x.Id == FinishedProduct.Id);
            RawMaterialA = await Db.Products.FirstAsync(x => x.Id == RawMaterialA.Id);
            RawMaterialB = await Db.Products.FirstAsync(x => x.Id == RawMaterialB.Id);
            Warehouse = await Db.Warehouses.FirstAsync(x => x.Id == Warehouse.Id);
            FgStock = await Db.StockBalances.FirstAsync(x => x.Id == FgStock.Id);
            RawStockA = await Db.StockBalances.FirstAsync(x => x.Id == RawStockA.Id);
            RawStockB = await Db.StockBalances.FirstAsync(x => x.Id == RawStockB.Id);
            Order = await Db.ProductionOrders.OrderByDescending(x => x.Id).FirstAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_tempDirectory)) Directory.Delete(_tempDirectory, true);
        }
    }
}
