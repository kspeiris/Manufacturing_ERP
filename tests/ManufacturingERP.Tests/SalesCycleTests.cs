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

public class SalesCycleTests
{
    [Fact]
    public async Task CreateCreditInvoice_WithMultipleLines_UpdatesOutstanding_And_ReducesStock()
    {
        await using var harness = await SalesTestHarness.CreateAsync();
        var salesService = harness.CreateSalesService();

        var request = new CreateInvoiceRequest
        {
            CustomerId = harness.Customer.Id,
            VehicleId = harness.Vehicle.Id,
            SaleType = SaleType.Credit,
            PaidAmount = 50m,
            Items =
            [
                new CreateInvoiceLineRequest { ProductId = harness.ProductA.Id, Quantity = 2, UnitPrice = 100m },
                new CreateInvoiceLineRequest { ProductId = harness.ProductB.Id, Quantity = 1, UnitPrice = 80m }
            ]
        };

        var result = await salesService.CreateInvoiceAsync(request);

        Assert.True(result.IsSuccess, result.Message);

        await harness.ReloadAsync();
        Assert.Equal(230m, harness.Customer.OutstandingBalance);
        Assert.Equal(8m, harness.StockA.QuantityOnHand);
        Assert.Equal(4m, harness.StockB.QuantityOnHand);
        Assert.Equal(2, await harness.Db.WarehouseTransactions.CountAsync(x => x.ReferenceNo == result.Message));
    }

    [Fact]
    public async Task CreateInvoice_WithInvalidQuantity_Or_ZeroAmount_Should_Fail()
    {
        await using var harness = await SalesTestHarness.CreateAsync();
        var salesService = harness.CreateSalesService();

        var invalidQuantity = await salesService.CreateInvoiceAsync(new CreateInvoiceRequest
        {
            CustomerId = harness.Customer.Id,
            VehicleId = harness.Vehicle.Id,
            SaleType = SaleType.Credit,
            PaidAmount = 0,
            Items = [new CreateInvoiceLineRequest { ProductId = harness.ProductA.Id, Quantity = 0, UnitPrice = 100m }]
        });

        var zeroAmount = await salesService.CreateInvoiceAsync(new CreateInvoiceRequest
        {
            CustomerId = harness.Customer.Id,
            VehicleId = harness.Vehicle.Id,
            SaleType = SaleType.Credit,
            PaidAmount = 0,
            Items = [new CreateInvoiceLineRequest { ProductId = harness.ProductA.Id, Quantity = 1, UnitPrice = 0m }]
        });

        Assert.False(invalidQuantity.IsSuccess);
        Assert.Contains("greater than zero", invalidQuantity.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(zeroAmount.IsSuccess);
        Assert.Contains("greater than zero", zeroAmount.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await harness.Db.SalesInvoices.CountAsync());
    }

    [Fact]
    public async Task CashInvoice_Should_Be_FullyPaid_And_NotIncreaseOutstanding()
    {
        await using var harness = await SalesTestHarness.CreateAsync();
        var salesService = harness.CreateSalesService();

        var partialCash = await salesService.CreateInvoiceAsync(new CreateInvoiceRequest
        {
            CustomerId = harness.Customer.Id,
            VehicleId = harness.Vehicle.Id,
            SaleType = SaleType.Cash,
            PaidAmount = 20m,
            Items = [new CreateInvoiceLineRequest { ProductId = harness.ProductA.Id, Quantity = 1, UnitPrice = 100m }]
        });

        Assert.False(partialCash.IsSuccess);

        var fullCash = await salesService.CreateInvoiceAsync(new CreateInvoiceRequest
        {
            CustomerId = harness.Customer.Id,
            VehicleId = harness.Vehicle.Id,
            SaleType = SaleType.Cash,
            PaidAmount = 100m,
            Items = [new CreateInvoiceLineRequest { ProductId = harness.ProductA.Id, Quantity = 1, UnitPrice = 100m }]
        });

        Assert.True(fullCash.IsSuccess, fullCash.Message);
        await harness.ReloadAsync();
        Assert.Equal(0m, harness.Customer.OutstandingBalance);
    }

    [Fact]
    public async Task RegisterCollection_Partial_Should_ReduceBalance_And_PreventOverCollection()
    {
        await using var harness = await SalesTestHarness.CreateAsync();
        var salesService = harness.CreateSalesService();
        harness.Customer.OutstandingBalance = 300m;
        await harness.Db.SaveChangesAsync();

        var partial = await salesService.RegisterCollectionAsync(harness.Customer.Id, 120m, "RCPT-001", "Partial");
        Assert.True(partial.IsSuccess, partial.Message);

        await harness.ReloadAsync();
        Assert.Equal(180m, harness.Customer.OutstandingBalance);

        var overCollection = await salesService.RegisterCollectionAsync(harness.Customer.Id, 181m, "RCPT-002", "Too much");
        Assert.False(overCollection.IsSuccess);
        Assert.Contains("cannot exceed", overCollection.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CustomerLedger_Should_ShowInvoice_And_Collection_InRunningOrder()
    {
        await using var harness = await SalesTestHarness.CreateAsync();
        var salesService = harness.CreateSalesService();
        var ledgerService = new LedgerService(harness.Db);

        var invoiceResult = await salesService.CreateInvoiceAsync(new CreateInvoiceRequest
        {
            CustomerId = harness.Customer.Id,
            VehicleId = harness.Vehicle.Id,
            SaleType = SaleType.Credit,
            PaidAmount = 20m,
            Items = [new CreateInvoiceLineRequest { ProductId = harness.ProductA.Id, Quantity = 1, UnitPrice = 100m }]
        });

        Assert.True(invoiceResult.IsSuccess, invoiceResult.Message);

        var collectionResult = await salesService.RegisterCollectionAsync(harness.Customer.Id, 30m, "RCPT-100", "Collection");
        Assert.True(collectionResult.IsSuccess, collectionResult.Message);

        var rows = await ledgerService.GetCustomerLedgerAsync(harness.Customer.Id);

        Assert.Equal(2, rows.Count);
        Assert.Equal("Credit Invoice", rows[0].EntryType);
        Assert.Equal(100m, rows[0].Debit);
        Assert.Equal(20m, rows[0].Credit);
        Assert.Equal(80m, rows[0].RunningBalance);
        Assert.Equal("Collection", rows[1].EntryType);
        Assert.Equal(30m, rows[1].Credit);
        Assert.Equal(50m, rows[1].RunningBalance);
    }

    private sealed class SalesTestHarness : IAsyncDisposable
    {
        private readonly string _tempDirectory;

        private SalesTestHarness(string tempDirectory, AppDbContext db, User user, Customer customer, Vehicle vehicle, Product productA, Product productB, StockBalance stockA, StockBalance stockB)
        {
            _tempDirectory = tempDirectory;
            Db = db;
            User = user;
            Customer = customer;
            Vehicle = vehicle;
            ProductA = productA;
            ProductB = productB;
            StockA = stockA;
            StockB = stockB;
        }

        public AppDbContext Db { get; }
        public User User { get; }
        public Customer Customer { get; private set; }
        public Vehicle Vehicle { get; private set; }
        public Product ProductA { get; private set; }
        public Product ProductB { get; private set; }
        public StockBalance StockA { get; private set; }
        public StockBalance StockB { get; private set; }

        public static async Task<SalesTestHarness> CreateAsync()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "ManufacturingERP.SalesTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            var databasePath = Path.Combine(tempDirectory, "sales-cycle.db");

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;

            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var customer = new Customer
            {
                CustomerCode = "C-TEST",
                ShopName = "Test Customer",
                OwnerName = "Owner",
                Route = "Route",
                CreditLimit = 1000m,
                OutstandingBalance = 0m,
                IsActive = true
            };

            var vehicle = new Vehicle
            {
                VehicleNumber = "V-001",
                Description = "Test Van",
                IsActive = true
            };

            var user = new User
            {
                Username = AppConstants.DefaultAdminUser,
                PasswordHash = "hash",
                FullName = "Admin User",
                Role = UserRole.Admin,
                IsActive = true
            };

            var warehouse = new Warehouse
            {
                Name = "Main Warehouse",
                Location = "Test",
                IsActive = true
            };

            var category = new ProductCategory { Name = "FG" };
            var productA = new Product { Code = "P-001", Name = "Product A", ProductCategory = category, Unit = "PCS", SellingPrice = 100m, CostPrice = 60m, IsActive = true };
            var productB = new Product { Code = "P-002", Name = "Product B", ProductCategory = category, Unit = "PCS", SellingPrice = 80m, CostPrice = 45m, IsActive = true };

            db.Customers.Add(customer);
            db.Vehicles.Add(vehicle);
            db.Users.Add(user);
            db.Warehouses.Add(warehouse);
            db.ProductCategories.Add(category);
            db.Products.AddRange(productA, productB);
            await db.SaveChangesAsync();

            var stockA = new StockBalance { ProductId = productA.Id, WarehouseId = warehouse.Id, QuantityOnHand = 10m };
            var stockB = new StockBalance { ProductId = productB.Id, WarehouseId = warehouse.Id, QuantityOnHand = 5m };
            db.StockBalances.AddRange(stockA, stockB);
            await db.SaveChangesAsync();

            return new SalesTestHarness(tempDirectory, db, user, customer, vehicle, productA, productB, stockA, stockB);
        }

        public SalesService CreateSalesService()
        {
            var currentUserService = new CurrentUserService();
            currentUserService.Set(User);
            return new SalesService(Db, new AuthorizationService(currentUserService), new AuditService(Db), currentUserService);
        }

        public async Task ReloadAsync()
        {
            Customer = await Db.Customers.FirstAsync(x => x.Id == Customer.Id);
            StockA = await Db.StockBalances.FirstAsync(x => x.Id == StockA.Id);
            StockB = await Db.StockBalances.FirstAsync(x => x.Id == StockB.Id);
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
