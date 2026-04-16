using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Application.Services;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Domain.Enums;
using ManufacturingERP.Infrastructure.Persistence;
using ManufacturingERP.Shared.Constants;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ManufacturingERP.Tests;

public class InventoryStockTests
{
    [Fact]
    public async Task OpeningStock_And_Adjustments_Should_UpdateBalance_And_PreventNegativeStock()
    {
        await using var harness = await InventoryTestHarness.CreateAsync();

        var opening = await harness.WarehouseService.CreateAdjustmentAsync(harness.Product.Id, harness.Warehouse.Id, 15m, "Opening stock");
        Assert.True(opening.IsSuccess, opening.Message);

        var zeroAdjustment = await harness.WarehouseService.CreateAdjustmentAsync(harness.Product.Id, harness.Warehouse.Id, 0m, "Zero");
        Assert.False(zeroAdjustment.IsSuccess);

        var noReason = await harness.WarehouseService.CreateAdjustmentAsync(harness.Product.Id, harness.Warehouse.Id, 2m, "");
        Assert.False(noReason.IsSuccess);

        var issue = await harness.WarehouseService.CreateAdjustmentAsync(harness.Product.Id, harness.Warehouse.Id, -4m, "Damaged");
        Assert.True(issue.IsSuccess, issue.Message);

        var tooMuch = await harness.WarehouseService.CreateAdjustmentAsync(harness.Product.Id, harness.Warehouse.Id, -20m, "Over issue");
        Assert.False(tooMuch.IsSuccess);
        Assert.Contains("negative stock", tooMuch.Message, StringComparison.OrdinalIgnoreCase);

        await harness.ReloadAsync();
        Assert.Equal(11m, harness.Stock.QuantityOnHand);

        var movements = await harness.WarehouseService.GetStockMovementsAsync(harness.Product.Id, harness.Warehouse.Id);
        Assert.Equal(2, movements.Count);
        Assert.Contains(movements, x => x.TransactionType == "ADJ-IN" && x.QuantityIn == 15m);
        Assert.Contains(movements, x => x.TransactionType == "ADJ-OUT" && x.QuantityOut == 4m);
    }

    [Fact]
    public async Task GRN_Sale_And_PurchaseReturn_Should_KeepStockTotalsAccurate()
    {
        await using var harness = await InventoryTestHarness.CreateAsync();

        var opening = await harness.WarehouseService.CreateAdjustmentAsync(harness.Product.Id, harness.Warehouse.Id, 20m, "Opening stock");
        Assert.True(opening.IsSuccess, opening.Message);

        var po = await harness.ProcurementService.CreatePurchaseOrderAsync(new CreatePurchaseOrderRequest
        {
            SupplierId = harness.Supplier.Id,
            Items = [new CreatePurchaseOrderLineRequest { ProductId = harness.Product.Id, Quantity = 5m, UnitPrice = 25m }]
        });
        Assert.True(po.IsSuccess, po.Message);

        var grn = await harness.ProcurementService.CreateGoodsReceiptAsync(new CreateGoodsReceiptRequest
        {
            SupplierId = harness.Supplier.Id,
            WarehouseId = harness.Warehouse.Id,
            PurchaseOrderNo = po.Message,
            Items = [new CreateGoodsReceiptLineRequest { ProductId = harness.Product.Id, Quantity = 5m, UnitCost = 25m }]
        });
        Assert.True(grn.IsSuccess, grn.Message);

        var sale = await harness.SalesService.CreateInvoiceAsync(new CreateInvoiceRequest
        {
            CustomerId = harness.Customer.Id,
            VehicleId = harness.Vehicle.Id,
            SaleType = SaleType.Cash,
            PaidAmount = 160m,
            Items = [new CreateInvoiceLineRequest { ProductId = harness.Product.Id, Quantity = 4m, UnitPrice = 40m }]
        });
        Assert.True(sale.IsSuccess, sale.Message);

        var purchaseReturn = await harness.Phase3ProcurementService.CreatePurchaseReturnAsync(new CreatePurchaseReturnRequest
        {
            SupplierId = harness.Supplier.Id,
            WarehouseId = harness.Warehouse.Id,
            Reason = "Damaged on receipt",
            Items = [new CreatePurchaseReturnLineRequest { ProductId = harness.Product.Id, Quantity = 3m, UnitCost = 25m }]
        });
        Assert.True(purchaseReturn.IsSuccess, purchaseReturn.Message);

        await harness.ReloadAsync();
        Assert.Equal(18m, harness.Stock.QuantityOnHand);

        var stockRows = await harness.WarehouseService.GetStockAsync();
        var row = Assert.Single(stockRows.Where(x => x.ProductCode == harness.Product.Code && x.WarehouseName == harness.Warehouse.Name));
        Assert.Equal(18m, row.QuantityOnHand);
        Assert.Equal(450m, row.StockValue);

        var movements = await harness.WarehouseService.GetStockMovementsAsync(harness.Product.Id, harness.Warehouse.Id);
        Assert.Contains(movements, x => x.TransactionType == "GRN" && x.QuantityIn == 5m);
        Assert.Contains(movements, x => x.TransactionType == "SALE-OUT" && x.QuantityOut == 4m);
        Assert.Contains(movements, x => x.TransactionType == "PUR-RETURN" && x.QuantityOut == 3m);
    }

    [Fact]
    public async Task VehicleLoad_Should_ReduceWarehouseStock_And_LogMovement()
    {
        await using var harness = await InventoryTestHarness.CreateAsync();

        var opening = await harness.WarehouseService.CreateAdjustmentAsync(harness.Product.Id, harness.Warehouse.Id, 12m, "Opening stock");
        Assert.True(opening.IsSuccess, opening.Message);

        var load = await harness.InventoryService.CreateVehicleLoadAsync(new LoadVehicleRequest
        {
            VehicleId = harness.Vehicle.Id,
            RouteName = "North Route",
            LoadDate = DateTime.Today,
            Items = [new LoadVehicleItemRequest { ProductId = harness.Product.Id, Quantity = 5m }]
        }, harness.Warehouse.Id);

        Assert.True(load.IsSuccess, load.Message);

        await harness.ReloadAsync();
        Assert.Equal(7m, harness.Stock.QuantityOnHand);

        var movement = (await harness.WarehouseService.GetStockMovementsAsync(harness.Product.Id, harness.Warehouse.Id))
            .FirstOrDefault(x => x.TransactionType == "VEH-LOAD");

        Assert.NotNull(movement);
        Assert.Equal(5m, movement!.QuantityOut);
    }

    private sealed class InventoryTestHarness : IAsyncDisposable
    {
        private readonly string _tempDirectory;

        private InventoryTestHarness(
            string tempDirectory,
            AppDbContext db,
            WarehouseService warehouseService,
            ProcurementService procurementService,
            Phase3ProcurementService phase3ProcurementService,
            SalesService salesService,
            InventoryService inventoryService,
            Product product,
            Warehouse warehouse,
            Supplier supplier,
            Customer customer,
            Vehicle vehicle,
            StockBalance stock)
        {
            _tempDirectory = tempDirectory;
            Db = db;
            WarehouseService = warehouseService;
            ProcurementService = procurementService;
            Phase3ProcurementService = phase3ProcurementService;
            SalesService = salesService;
            InventoryService = inventoryService;
            Product = product;
            Warehouse = warehouse;
            Supplier = supplier;
            Customer = customer;
            Vehicle = vehicle;
            Stock = stock;
        }

        public AppDbContext Db { get; }
        public WarehouseService WarehouseService { get; }
        public ProcurementService ProcurementService { get; }
        public Phase3ProcurementService Phase3ProcurementService { get; }
        public SalesService SalesService { get; }
        public InventoryService InventoryService { get; }
        public Product Product { get; private set; }
        public Warehouse Warehouse { get; private set; }
        public Supplier Supplier { get; private set; }
        public Customer Customer { get; private set; }
        public Vehicle Vehicle { get; private set; }
        public StockBalance Stock { get; private set; }

        public static async Task<InventoryTestHarness> CreateAsync()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "ManufacturingERP.InventoryTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            var databasePath = Path.Combine(tempDirectory, "inventory-stock.db");

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;

            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var user = new User
            {
                Username = AppConstants.DefaultAdminUser,
                PasswordHash = "hash",
                FullName = "Admin User",
                Role = UserRole.Admin,
                IsActive = true
            };

            var supplier = new Supplier
            {
                SupplierCode = "SUP-STK",
                Name = "Stock Supplier",
                ContactNumber = "0770000000",
                Address = "Warehouse Road"
            };

            var customer = new Customer
            {
                CustomerCode = "CUS-STK",
                ShopName = "Stock Customer",
                OwnerName = "Owner",
                Route = "Route 1",
                CreditLimit = 1000m,
                OutstandingBalance = 0m,
                IsActive = true
            };

            var vehicle = new Vehicle
            {
                VehicleNumber = "VEH-STK",
                Description = "Stock Van",
                IsActive = true
            };

            var warehouse = new Warehouse
            {
                Name = "Main Warehouse",
                Location = "Central",
                IsActive = true
            };

            var category = new ProductCategory { Name = "FG" };
            var product = new Product
            {
                Code = "P-STK",
                Name = "Stock Product",
                ProductCategory = category,
                Unit = "PCS",
                CostPrice = 25m,
                SellingPrice = 40m,
                IsActive = true
            };

            db.Users.Add(user);
            db.Suppliers.Add(supplier);
            db.Customers.Add(customer);
            db.Vehicles.Add(vehicle);
            db.Warehouses.Add(warehouse);
            db.ProductCategories.Add(category);
            db.Products.Add(product);
            await db.SaveChangesAsync();

            var stock = new StockBalance { ProductId = product.Id, WarehouseId = warehouse.Id, QuantityOnHand = 0m };
            db.StockBalances.Add(stock);
            await db.SaveChangesAsync();

            var currentUserService = new CurrentUserService();
            currentUserService.Set(user);
            var authorizationService = new AuthorizationService(currentUserService);
            var auditService = new AuditService(db);

            return new InventoryTestHarness(
                tempDirectory,
                db,
                new WarehouseService(db, authorizationService, auditService, currentUserService),
                new ProcurementService(db, authorizationService, auditService, currentUserService),
                new Phase3ProcurementService(db, authorizationService, auditService, currentUserService),
                new SalesService(db, authorizationService, auditService, currentUserService),
                new InventoryService(db),
                product,
                warehouse,
                supplier,
                customer,
                vehicle,
                stock);
        }

        public async Task ReloadAsync()
        {
            Product = await Db.Products.FirstAsync(x => x.Id == Product.Id);
            Warehouse = await Db.Warehouses.FirstAsync(x => x.Id == Warehouse.Id);
            Supplier = await Db.Suppliers.FirstAsync(x => x.Id == Supplier.Id);
            Customer = await Db.Customers.FirstAsync(x => x.Id == Customer.Id);
            Vehicle = await Db.Vehicles.FirstAsync(x => x.Id == Vehicle.Id);
            Stock = await Db.StockBalances.FirstAsync(x => x.Id == Stock.Id);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_tempDirectory))
                Directory.Delete(_tempDirectory, true);
        }
    }
}
