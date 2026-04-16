using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Application.Services;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Domain.Enums;
using ManufacturingERP.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ManufacturingERP.Tests;

public class ReportsPrintingTests
{
    [Fact]
    public async Task ReportingService_Should_ReturnRowsMatchingDatabase()
    {
        await using var harness = await ReportsTestHarness.CreateAsync();

        var salesRows = await harness.ReportingService.GetSalesRegisterAsync(DateTime.Today.AddDays(-7), DateTime.Today);
        var purchaseRows = await harness.ReportingService.GetPurchaseRegisterAsync(DateTime.Today.AddDays(-7), DateTime.Today);
        var stockRows = await harness.ReportingService.GetStockReportAsync();
        var productionRows = await harness.ReportingService.GetProductionReportAsync(DateTime.Today.AddDays(-7), DateTime.Today);

        Assert.Single(salesRows);
        Assert.Equal("INV-001", salesRows[0].InvoiceNo);
        Assert.Equal(240m, salesRows[0].TotalAmount);

        Assert.Single(purchaseRows);
        Assert.Equal("PO-001", purchaseRows[0].OrderNo);
        Assert.Equal(300m, purchaseRows[0].TotalAmount);

        Assert.Single(stockRows);
        Assert.Equal("FG-001", stockRows[0].ProductCode);
        Assert.Equal(18m, stockRows[0].QuantityOnHand);
        Assert.Equal(900m, stockRows[0].StockValue);

        Assert.Single(productionRows);
        Assert.Equal("PROD-001", productionRows[0].OrderNo);
        Assert.Equal(18m, productionRows[0].ProducedQuantity);
        Assert.Equal(50m, productionRows[0].UnitCost);
    }

    [Fact]
    public async Task PrintingService_Should_CreatePrintableHtmlFiles()
    {
        await using var harness = await ReportsTestHarness.CreateAsync();

        var salesRows = await harness.ReportingService.GetSalesRegisterAsync(DateTime.Today.AddDays(-7), DateTime.Today);
        var salesPath = Path.Combine(harness.TempDirectory, "sales_register.html");
        var salesResult = await harness.PrintingService.ExportHtmlTableReportAsync(
            new PrintReportRequest
            {
                ReportName = "Sales Register",
                OutputPath = salesPath,
                FilterText = "Current week"
            },
            new[] { "Invoice", "Customer", "Amount" },
            salesRows.Select(x => (IReadOnlyList<string>)new[] { x.InvoiceNo, x.CustomerName, x.TotalAmount.ToString("N2") }),
            "Weekly sales");

        var invoicePath = Path.Combine(harness.TempDirectory, "invoice.html");
        var poPath = Path.Combine(harness.TempDirectory, "po.html");

        var invoiceResult = await harness.PrintingService.ExportLatestInvoiceHtmlAsync(invoicePath);
        var poResult = await harness.PrintingService.ExportLatestPurchaseOrderHtmlAsync(poPath);

        Assert.True(salesResult.IsSuccess, salesResult.Message);
        Assert.True(invoiceResult.IsSuccess, invoiceResult.Message);
        Assert.True(poResult.IsSuccess, poResult.Message);

        Assert.True(File.Exists(salesPath));
        Assert.True(File.Exists(invoicePath));
        Assert.True(File.Exists(poPath));

        var salesHtml = await File.ReadAllTextAsync(salesPath);
        var invoiceHtml = await File.ReadAllTextAsync(invoicePath);
        var poHtml = await File.ReadAllTextAsync(poPath);

        Assert.Contains("<html>", salesHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Sales Register", salesHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("INV-001", invoiceHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Sales Invoice", invoiceHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PO-001", poHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Purchase Order", poHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StatementExports_Should_RespectDateFilters()
    {
        await using var harness = await ReportsTestHarness.CreateAsync();

        var fromDate = DateTime.Today.AddDays(-7);
        var toDate = DateTime.Today;

        var customerRows = await harness.LedgerService.GetCustomerLedgerAsync(harness.Customer.Id, fromDate, toDate);
        var supplierRows = await harness.LedgerService.GetSupplierLedgerAsync(harness.Supplier.Id, fromDate, toDate);

        Assert.Equal(2, customerRows.Count);
        Assert.Contains(customerRows, x => x.ReferenceNo == "INV-001");
        Assert.Contains(customerRows, x => x.ReferenceNo == "RCPT-NEW");
        Assert.DoesNotContain(customerRows, x => x.ReferenceNo == "INV-OLD");

        Assert.Equal(2, supplierRows.Count);
        Assert.Contains(supplierRows, x => x.ReferenceNo == "SINV-001");
        Assert.Contains(supplierRows, x => x.ReferenceNo == "SPAY-NEW");
        Assert.DoesNotContain(supplierRows, x => x.ReferenceNo == "SINV-OLD");

        var customerStatementPath = Path.Combine(harness.TempDirectory, "customer_statement.html");
        var supplierStatementPath = Path.Combine(harness.TempDirectory, "supplier_statement.html");

        var customerResult = await harness.PrintingService.ExportCustomerStatementHtmlAsync(
            customerStatementPath,
            harness.Customer.ShopName,
            customerRows,
            fromDate,
            toDate);

        var supplierResult = await harness.PrintingService.ExportSupplierStatementHtmlAsync(
            supplierStatementPath,
            harness.Supplier.Name,
            supplierRows,
            fromDate,
            toDate);

        Assert.True(customerResult.IsSuccess, customerResult.Message);
        Assert.True(supplierResult.IsSuccess, supplierResult.Message);

        var customerHtml = await File.ReadAllTextAsync(customerStatementPath);
        var supplierHtml = await File.ReadAllTextAsync(supplierStatementPath);

        Assert.Contains("RCPT-NEW", customerHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INV-OLD", customerHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SPAY-NEW", supplierHtml, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SINV-OLD", supplierHtml, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ReportsTestHarness : IAsyncDisposable
    {
        private ReportsTestHarness(
            string tempDirectory,
            AppDbContext db,
            ReportingService reportingService,
            PrintingService printingService,
            LedgerService ledgerService,
            Customer customer,
            Supplier supplier)
        {
            TempDirectory = tempDirectory;
            Db = db;
            ReportingService = reportingService;
            PrintingService = printingService;
            LedgerService = ledgerService;
            Customer = customer;
            Supplier = supplier;
        }

        public string TempDirectory { get; }
        public AppDbContext Db { get; }
        public ReportingService ReportingService { get; }
        public PrintingService PrintingService { get; }
        public LedgerService LedgerService { get; }
        public Customer Customer { get; }
        public Supplier Supplier { get; }

        public static async Task<ReportsTestHarness> CreateAsync()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "ManufacturingERP.ReportsTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            var databasePath = Path.Combine(tempDirectory, "reports.db");

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;

            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var customer = new Customer
            {
                CustomerCode = "C001",
                ShopName = "North Stores",
                OwnerName = "Nimal",
                Route = "Route 1",
                CreditLimit = 1000m,
                OutstandingBalance = 150m,
                IsActive = true
            };

            var supplier = new Supplier
            {
                SupplierCode = "S001",
                Name = "Best Supplier",
                ContactNumber = "0110000000",
                Address = "Colombo"
            };

            var vehicle = new Vehicle
            {
                VehicleNumber = "V-001",
                Description = "Delivery Van",
                IsActive = true
            };

            var warehouse = new Warehouse
            {
                Name = "Main Warehouse",
                Location = "Factory",
                IsActive = true
            };

            var category = new ProductCategory { Name = "Finished Goods" };
            var product = new Product
            {
                Code = "FG-001",
                Name = "Finished Product",
                ProductCategory = category,
                Unit = "PCS",
                CostPrice = 50m,
                SellingPrice = 80m,
                IsActive = true
            };

            db.Customers.Add(customer);
            db.Suppliers.Add(supplier);
            db.Vehicles.Add(vehicle);
            db.Warehouses.Add(warehouse);
            db.ProductCategories.Add(category);
            db.Products.Add(product);
            await db.SaveChangesAsync();

            db.StockBalances.Add(new StockBalance
            {
                ProductId = product.Id,
                WarehouseId = warehouse.Id,
                QuantityOnHand = 18m
            });

            db.SalesInvoices.AddRange(
                new SalesInvoice
                {
                    InvoiceNo = "INV-OLD",
                    InvoiceDate = DateTime.Today.AddDays(-20),
                    CustomerId = customer.Id,
                    VehicleId = vehicle.Id,
                    SaleType = SaleType.Credit,
                    TotalAmount = 100m,
                    PaidAmount = 0m,
                    Items = new List<SalesInvoiceItem>
                    {
                        new() { ProductId = product.Id, Quantity = 2m, UnitPrice = 50m }
                    }
                },
                new SalesInvoice
                {
                    InvoiceNo = "INV-001",
                    InvoiceDate = DateTime.Today.AddDays(-1),
                    CustomerId = customer.Id,
                    VehicleId = vehicle.Id,
                    SaleType = SaleType.Credit,
                    TotalAmount = 240m,
                    PaidAmount = 40m,
                    Items = new List<SalesInvoiceItem>
                    {
                        new() { ProductId = product.Id, Quantity = 3m, UnitPrice = 80m }
                    }
                });

            db.CollectionEntries.AddRange(
                new CollectionEntry
                {
                    CustomerId = customer.Id,
                    Amount = 30m,
                    ReferenceNo = "RCPT-OLD",
                    Notes = "Old collection",
                    CollectionDate = DateTime.Today.AddDays(-15)
                },
                new CollectionEntry
                {
                    CustomerId = customer.Id,
                    Amount = 40m,
                    ReferenceNo = "RCPT-NEW",
                    Notes = "Recent collection",
                    CollectionDate = DateTime.Today.AddDays(-1)
                });

            db.PurchaseOrders.Add(new PurchaseOrder
            {
                OrderNo = "PO-001",
                OrderDate = DateTime.Today.AddDays(-2),
                SupplierId = supplier.Id,
                Status = "Closed",
                TotalAmount = 300m,
                Items = new List<PurchaseOrderItem>
                {
                    new() { ProductId = product.Id, Quantity = 6m, UnitPrice = 50m }
                }
            });

            db.SupplierInvoices.AddRange(
                new SupplierInvoice
                {
                    InvoiceNo = "SINV-OLD",
                    InvoiceDate = DateTime.Today.AddDays(-18),
                    SupplierId = supplier.Id,
                    TotalAmount = 120m,
                    PaidAmount = 0m,
                    BalanceAmount = 120m,
                    Status = "Open"
                },
                new SupplierInvoice
                {
                    InvoiceNo = "SINV-001",
                    InvoiceDate = DateTime.Today.AddDays(-2),
                    SupplierId = supplier.Id,
                    ReferencePoNo = "PO-001",
                    TotalAmount = 300m,
                    PaidAmount = 100m,
                    BalanceAmount = 200m,
                    Status = "Partially Paid"
                });

            db.SupplierPayments.AddRange(
                new SupplierPayment
                {
                    PaymentNo = "SPAY-OLD",
                    PaymentDate = DateTime.Today.AddDays(-15),
                    SupplierId = supplier.Id,
                    ReferenceInvoiceNo = "SINV-OLD",
                    Amount = 20m,
                    PaymentMethod = "Cash"
                },
                new SupplierPayment
                {
                    PaymentNo = "SPAY-NEW",
                    PaymentDate = DateTime.Today.AddDays(-1),
                    SupplierId = supplier.Id,
                    ReferenceInvoiceNo = "SINV-001",
                    Amount = 100m,
                    PaymentMethod = "Cash"
                });

            db.ProductionOrders.Add(new ProductionOrder
            {
                OrderNo = "PROD-001",
                OrderDate = DateTime.Today.AddDays(-1),
                FinishedProductId = product.Id,
                PlannedQuantity = 20m,
                ProducedQuantity = 18m,
                ScrapQuantity = 2m,
                MaterialCost = 600m,
                LaborCost = 180m,
                OverheadCost = 120m,
                BatchNo = "BATCH-001",
                Status = "Completed"
            });

            await db.SaveChangesAsync();

            return new ReportsTestHarness(
                tempDirectory,
                db,
                new ReportingService(db),
                new PrintingService(db),
                new LedgerService(db),
                customer,
                supplier);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(TempDirectory))
                Directory.Delete(TempDirectory, true);
        }
    }
}
