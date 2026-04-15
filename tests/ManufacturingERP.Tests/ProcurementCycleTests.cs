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

public class ProcurementCycleTests
{
    [Fact]
    public async Task ProcurementCycle_EndToEnd_Should_UpdateStock_InvoiceBalance_And_SupplierLedger()
    {
        await using var harness = await ProcurementTestHarness.CreateAsync();

        var poResult = await harness.ProcurementService.CreatePurchaseOrderAsync(new CreatePurchaseOrderRequest
        {
            SupplierId = harness.Supplier.Id,
            Notes = "PO for test",
            Items =
            [
                new CreatePurchaseOrderLineRequest { ProductId = harness.Product.Id, Quantity = 10m, UnitPrice = 25m }
            ]
        });

        Assert.True(poResult.IsSuccess, poResult.Message);

        var grnResult = await harness.ProcurementService.CreateGoodsReceiptAsync(new CreateGoodsReceiptRequest
        {
            SupplierId = harness.Supplier.Id,
            WarehouseId = harness.Warehouse.Id,
            PurchaseOrderNo = poResult.Message,
            Notes = "Receipt",
            Items =
            [
                new CreateGoodsReceiptLineRequest { ProductId = harness.Product.Id, Quantity = 10m, UnitCost = 25m }
            ]
        });

        Assert.True(grnResult.IsSuccess, grnResult.Message);

        var invoiceResult = await harness.Phase3ProcurementService.CreateSupplierInvoiceAsync(new CreateSupplierInvoiceRequest
        {
            SupplierId = harness.Supplier.Id,
            ReferencePoNo = poResult.Message,
            ReferenceGrnNo = grnResult.Message,
            TotalAmount = 250m,
            PaidAmount = 50m,
            DueDate = DateTime.Today.AddDays(14),
            Notes = "Supplier invoice"
        });

        Assert.True(invoiceResult.IsSuccess, invoiceResult.Message);

        var paymentResult = await harness.SupplierPaymentService.SaveAsync(new CreateSupplierPaymentRequest
        {
            SupplierId = harness.Supplier.Id,
            ReferenceInvoiceNo = invoiceResult.Message,
            Amount = 120m,
            PaymentMethod = "Bank",
            Notes = "Partial settlement"
        });

        Assert.True(paymentResult.IsSuccess, paymentResult.Message);

        var returnResult = await harness.Phase3ProcurementService.CreatePurchaseReturnAsync(new CreatePurchaseReturnRequest
        {
            SupplierId = harness.Supplier.Id,
            WarehouseId = harness.Warehouse.Id,
            ReferenceInvoiceNo = invoiceResult.Message,
            Reason = "Damaged bags",
            Items =
            [
                new CreatePurchaseReturnLineRequest { ProductId = harness.Product.Id, Quantity = 3m, UnitCost = 25m }
            ]
        });

        Assert.True(returnResult.IsSuccess, returnResult.Message);

        await harness.ReloadAsync();

        Assert.Equal(7m, harness.Stock.QuantityOnHand);
        Assert.Equal("Closed", harness.PurchaseOrder.Status);
        Assert.Equal(170m, harness.SupplierInvoice.PaidAmount);
        Assert.Equal(80m, harness.SupplierInvoice.BalanceAmount);
        Assert.Equal("Partially Paid", harness.SupplierInvoice.Status);
        Assert.Equal(2, await harness.Db.WarehouseTransactions.CountAsync(x => x.ProductId == harness.Product.Id));

        var ledgerRows = await harness.LedgerService.GetSupplierLedgerAsync(harness.Supplier.Id);

        Assert.Equal(2, ledgerRows.Count);
        Assert.Equal("Supplier Invoice", ledgerRows[0].EntryType);
        Assert.Equal(250m, ledgerRows[0].Debit);
        Assert.Equal(50m, ledgerRows[0].Credit);
        Assert.Equal(200m, ledgerRows[0].RunningBalance);
        Assert.Equal("Supplier Payment", ledgerRows[1].EntryType);
        Assert.Equal(120m, ledgerRows[1].Credit);
        Assert.Equal(80m, ledgerRows[1].RunningBalance);
    }

    [Fact]
    public async Task SupplierPayment_Should_Prevent_Overpayment()
    {
        await using var harness = await ProcurementTestHarness.CreateAsync();

        var invoiceResult = await harness.Phase3ProcurementService.CreateSupplierInvoiceAsync(new CreateSupplierInvoiceRequest
        {
            SupplierId = harness.Supplier.Id,
            TotalAmount = 100m,
            PaidAmount = 20m,
            DueDate = DateTime.Today.AddDays(7)
        });

        Assert.True(invoiceResult.IsSuccess, invoiceResult.Message);

        var result = await harness.SupplierPaymentService.SaveAsync(new CreateSupplierPaymentRequest
        {
            SupplierId = harness.Supplier.Id,
            ReferenceInvoiceNo = invoiceResult.Message,
            Amount = 81m,
            PaymentMethod = "Cash"
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("cannot exceed", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GoodsReceipt_Should_Not_Allow_MoreThan_PurchaseOrder_Quantity()
    {
        await using var harness = await ProcurementTestHarness.CreateAsync();

        var poResult = await harness.ProcurementService.CreatePurchaseOrderAsync(new CreatePurchaseOrderRequest
        {
            SupplierId = harness.Supplier.Id,
            Items =
            [
                new CreatePurchaseOrderLineRequest { ProductId = harness.Product.Id, Quantity = 5m, UnitPrice = 25m }
            ]
        });

        Assert.True(poResult.IsSuccess, poResult.Message);

        var result = await harness.ProcurementService.CreateGoodsReceiptAsync(new CreateGoodsReceiptRequest
        {
            SupplierId = harness.Supplier.Id,
            WarehouseId = harness.Warehouse.Id,
            PurchaseOrderNo = poResult.Message,
            Items =
            [
                new CreateGoodsReceiptLineRequest { ProductId = harness.Product.Id, Quantity = 6m, UnitCost = 25m }
            ]
        });

        Assert.False(result.IsSuccess);
        Assert.Contains("exceeds ordered quantity", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ProcurementTestHarness : IAsyncDisposable
    {
        private readonly string _tempDirectory;

        private ProcurementTestHarness(
            string tempDirectory,
            AppDbContext db,
            Supplier supplier,
            Warehouse warehouse,
            Product product,
            StockBalance stock,
            ProcurementService procurementService,
            Phase3ProcurementService phase3ProcurementService,
            SupplierPaymentService supplierPaymentService,
            LedgerService ledgerService)
        {
            _tempDirectory = tempDirectory;
            Db = db;
            Supplier = supplier;
            Warehouse = warehouse;
            Product = product;
            Stock = stock;
            ProcurementService = procurementService;
            Phase3ProcurementService = phase3ProcurementService;
            SupplierPaymentService = supplierPaymentService;
            LedgerService = ledgerService;
        }

        public AppDbContext Db { get; }
        public Supplier Supplier { get; private set; }
        public Warehouse Warehouse { get; private set; }
        public Product Product { get; private set; }
        public StockBalance Stock { get; private set; }
        public PurchaseOrder PurchaseOrder { get; private set; } = null!;
        public SupplierInvoice SupplierInvoice { get; private set; } = null!;
        public ProcurementService ProcurementService { get; }
        public Phase3ProcurementService Phase3ProcurementService { get; }
        public SupplierPaymentService SupplierPaymentService { get; }
        public LedgerService LedgerService { get; }

        public static async Task<ProcurementTestHarness> CreateAsync()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "ManufacturingERP.ProcurementTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            var databasePath = Path.Combine(tempDirectory, "procurement-cycle.db");

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
                SupplierCode = "SUP-001",
                Name = "Test Supplier",
                ContactNumber = "0770000000",
                Address = "Test Address"
            };

            var warehouse = new Warehouse
            {
                Name = "Main Warehouse",
                Location = "Test",
                IsActive = true
            };

            var category = new ProductCategory { Name = "RM" };
            var product = new Product
            {
                Code = "RM-001",
                Name = "Raw Material",
                ProductCategory = category,
                Unit = "PCS",
                SellingPrice = 35m,
                CostPrice = 25m,
                IsActive = true
            };

            db.Users.Add(user);
            db.Suppliers.Add(supplier);
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

            return new ProcurementTestHarness(
                tempDirectory,
                db,
                supplier,
                warehouse,
                product,
                stock,
                new ProcurementService(db),
                new Phase3ProcurementService(db, auditService),
                new SupplierPaymentService(db, auditService, authorizationService),
                new LedgerService(db));
        }

        public async Task ReloadAsync()
        {
            Supplier = await Db.Suppliers.FirstAsync(x => x.Id == Supplier.Id);
            Warehouse = await Db.Warehouses.FirstAsync(x => x.Id == Warehouse.Id);
            Product = await Db.Products.FirstAsync(x => x.Id == Product.Id);
            Stock = await Db.StockBalances.FirstAsync(x => x.Id == Stock.Id);
            PurchaseOrder = await Db.PurchaseOrders.OrderByDescending(x => x.Id).FirstAsync();
            SupplierInvoice = await Db.SupplierInvoices.OrderByDescending(x => x.Id).FirstAsync();
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
