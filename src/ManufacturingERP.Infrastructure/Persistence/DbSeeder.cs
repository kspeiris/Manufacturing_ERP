using ManufacturingERP.Application.Services;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Domain.Enums;
using ManufacturingERP.Shared.Constants;
using System.Security.Cryptography;

namespace ManufacturingERP.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        var hasher = new PasswordHasherService();

        if (!db.Users.Any())
        {
            var initialPassword = GetInitialAdminPassword();
            db.Users.AddRange(
                new User { Username = AppConstants.DefaultAdminUser, PasswordHash = hasher.Hash(initialPassword), FullName = "System Administrator", Role = UserRole.Admin }
            );

            WriteFirstRunPasswordFile(initialPassword);
        }
        else
        {
            EnsureFirstRunAdminPasswordMatchesFile(db, hasher);
        }


        if (!db.Accounts.Any())
        {
            db.Accounts.AddRange(
                new Account { AccountCode = "1000", AccountName = "Cash", AccountType = "Asset" },
                new Account { AccountCode = "1100", AccountName = "Accounts Receivable", AccountType = "Asset" },
                new Account { AccountCode = "1200", AccountName = "Inventory", AccountType = "Asset" },
                new Account { AccountCode = "2000", AccountName = "Accounts Payable", AccountType = "Liability" },
                new Account { AccountCode = "3000", AccountName = "Capital", AccountType = "Equity" },
                new Account { AccountCode = "4000", AccountName = "Sales Revenue", AccountType = "Revenue" },
                new Account { AccountCode = "5000", AccountName = "Cost of Sales", AccountType = "Cost" },
                new Account { AccountCode = "6000", AccountName = "Operating Expenses", AccountType = "Expense" }
            );
        }

        if (!db.ProductCategories.Any())
        {
            var fg = new ProductCategory { Name = "Finished Goods" };
            var rm = new ProductCategory { Name = "Raw Materials" };
            db.ProductCategories.AddRange(fg, rm);
            await db.SaveChangesAsync();
        }

        if (!db.Products.Any())
        {
            var fg = db.ProductCategories.First(c => c.Name == "Finished Goods");
            var rm = db.ProductCategories.First(c => c.Name == "Raw Materials");

            db.Products.AddRange(
                new Product { Code = "FG-001", Name = "Sample Product A", ProductCategoryId = fg.Id, CostPrice = 100, SellingPrice = 140, Unit = "PCS" },
                new Product { Code = "FG-002", Name = "Sample Product B", ProductCategoryId = fg.Id, CostPrice = 80, SellingPrice = 120, Unit = "PCS", ReorderLevel = 50 },
                new Product { Code = "FG-003", Name = "Premium Product C", ProductCategoryId = fg.Id, CostPrice = 150, SellingPrice = 220, Unit = "PCS", ReorderLevel = 30 },
                new Product { Code = "FG-004", Name = "Economy Product D", ProductCategoryId = fg.Id, CostPrice = 50, SellingPrice = 75, Unit = "PCS", ReorderLevel = 100 },
                new Product { Code = "FG-005", Name = "Bulk Product E", ProductCategoryId = fg.Id, CostPrice = 200, SellingPrice = 280, Unit = "BOX", ReorderLevel = 20 },
                new Product { Code = "RM-001", Name = "Raw Material X", ProductCategoryId = rm.Id, CostPrice = 30, SellingPrice = 0, Unit = "KG", ReorderLevel = 100, TrackBatch = true },
                new Product { Code = "RM-002", Name = "Packing Material Y", ProductCategoryId = rm.Id, CostPrice = 10, SellingPrice = 0, Unit = "PCS", ReorderLevel = 100 },
                new Product { Code = "RM-003", Name = "Chemical Z", ProductCategoryId = rm.Id, CostPrice = 45, SellingPrice = 0, Unit = "LIT", ReorderLevel = 50, TrackBatch = true },
                new Product { Code = "RM-004", Name = "Base Powder W", ProductCategoryId = rm.Id, CostPrice = 20, SellingPrice = 0, Unit = "KG", ReorderLevel = 200, TrackBatch = true },
                new Product { Code = "RM-005", Name = "Cardboard Box", ProductCategoryId = rm.Id, CostPrice = 5, SellingPrice = 0, Unit = "PCS", ReorderLevel = 500 }
            );
            await db.SaveChangesAsync();
        }

        if (!db.RoutePlans.Any())
            db.RoutePlans.AddRange(
                new RoutePlan { Code = "R001", Name = "Colombo Town Route", Territory = "Colombo" },
                new RoutePlan { Code = "R002", Name = "Gampaha Route", Territory = "Gampaha" },
                new RoutePlan { Code = "R003", Name = "Kandy Hill Route", Territory = "Kandy" },
                new RoutePlan { Code = "R004", Name = "Galle Coastal", Territory = "Galle" },
                new RoutePlan { Code = "R005", Name = "Negombo Suburbs", Territory = "Negombo" }
            );

        if (!db.Customers.Any())
            db.Customers.AddRange(
                new Customer { CustomerCode = "C001", ShopName = "Nimal Stores", OwnerName = "Nimal", Route = "Colombo Town Route", Address = "Colombo", ContactNumber = "0771234567", CreditLimit = 50000, OutstandingBalance = 18000 },
                new Customer { CustomerCode = "C002", ShopName = "Kamal Traders", OwnerName = "Kamal", Route = "Gampaha Route", Address = "Gampaha", ContactNumber = "0777654321", CreditLimit = 40000, OutstandingBalance = 9500 },
                new Customer { CustomerCode = "C003", ShopName = "Saman Grocery", OwnerName = "Saman", Route = "Kandy Hill Route", Address = "Kandy", ContactNumber = "0812234567", CreditLimit = 60000, OutstandingBalance = 0 },
                new Customer { CustomerCode = "C004", ShopName = "Ruwan Mini Mart", OwnerName = "Ruwan", Route = "Galle Coastal", Address = "Galle", ContactNumber = "0917654321", CreditLimit = 30000, OutstandingBalance = 12500 },
                new Customer { CustomerCode = "C005", ShopName = "Perera Super", OwnerName = "Amal Perera", Route = "Negombo Suburbs", Address = "Negombo", ContactNumber = "0311234567", CreditLimit = 100000, OutstandingBalance = 45000 },
                new Customer { CustomerCode = "C006", ShopName = "Silva Distributors", OwnerName = "Sarath Silva", Route = "Colombo Town Route", Address = "Bambalapitiya", ContactNumber = "0112233445", CreditLimit = 200000, OutstandingBalance = 85000 },
                new Customer { CustomerCode = "C007", ShopName = "Lanka City Food", OwnerName = "Nayana", Route = "Gampaha Route", Address = "Kadawatha", ContactNumber = "0712233445", CreditLimit = 45000, OutstandingBalance = 2000 },
                new Customer { CustomerCode = "C008", ShopName = "Kandy Traders", OwnerName = "Nuwan", Route = "Kandy Hill Route", Address = "Peradeniya", ContactNumber = "0811234567", CreditLimit = 50000, OutstandingBalance = 25000 },
                new Customer { CustomerCode = "C009", ShopName = "Galle City Stores", OwnerName = "Chathura", Route = "Galle Coastal", Address = "Unawatuna", ContactNumber = "0911234567", CreditLimit = 35000, OutstandingBalance = 5000 },
                new Customer { CustomerCode = "C010", ShopName = "Fernando Bros", OwnerName = "Roshan", Route = "Negombo Suburbs", Address = "Kochchikade", ContactNumber = "0317654321", CreditLimit = 75000, OutstandingBalance = 15000 }
            );

        if (!db.Suppliers.Any())
            db.Suppliers.AddRange(
                new Supplier { SupplierCode = "S001", Name = "ABC Raw Materials", ContactNumber = "0112233445", Address = "Colombo" },
                new Supplier { SupplierCode = "S002", Name = "Premier Packaging", ContactNumber = "0116677889", Address = "Ja-Ela" },
                new Supplier { SupplierCode = "S003", Name = "Global Chemicals", ContactNumber = "0113344556", Address = "Kelaniya" },
                new Supplier { SupplierCode = "S004", Name = "National Plastics", ContactNumber = "0117788990", Address = "Maharagama" },
                new Supplier { SupplierCode = "S005", Name = "Island wide Transports", ContactNumber = "0772233445", Address = "Pettah" }
            );

        if (!db.Vehicles.Any())
            db.Vehicles.Add(new Vehicle { VehicleNumber = "CAB-1234", Description = "Van 01", DriverName = "Sunil", SalesRepName = "Kasun" });

        if (!db.Warehouses.Any())
        {
            db.Warehouses.AddRange(
                new Warehouse { Name = "Main Warehouse", Location = "Factory Premises" },
                new Warehouse { Name = "Transit Warehouse", Location = "Distribution Yard" }
            );
            await db.SaveChangesAsync();
        }

        if (!db.WarehouseBins.Any())
        {
            var mainWarehouse = db.Warehouses.First();
            db.WarehouseBins.AddRange(
                new WarehouseBin { WarehouseId = mainWarehouse.Id, BinCode = "MAIN-RECV", Description = "Default receiving bin", Aisle = "MAIN", Rack = "RECV" },
                new WarehouseBin { WarehouseId = mainWarehouse.Id, BinCode = "MAIN-PICK", Description = "Default picking bin", Aisle = "MAIN", Rack = "PICK" }
            );
            await db.SaveChangesAsync();
        }

        await db.SaveChangesAsync();

        if (!db.StockBalances.Any())
        {
            var warehouseId = db.Warehouses.First().Id;
            foreach (var product in db.Products)
                db.StockBalances.Add(new StockBalance { ProductId = product.Id, WarehouseId = warehouseId, QuantityOnHand = product.Code.StartsWith("RM-") ? 500 : 250 });
        }

        if (!db.BatchLots.Any())
        {
            var warehouse = db.Warehouses.First();
            var bin = db.WarehouseBins.FirstOrDefault(x => x.WarehouseId == warehouse.Id);
            foreach (var product in db.Products.Where(x => x.TrackBatch))
            {
                db.BatchLots.Add(new BatchLot
                {
                    LotNumber = $"{product.Code}-LOT-001",
                    ProductId = product.Id,
                    WarehouseId = warehouse.Id,
                    WarehouseBinId = bin?.Id,
                    ManufacturingDate = DateTime.Today.AddDays(-30),
                    ExpiryDate = DateTime.Today.AddMonths(6),
                    QuantityReceived = 500,
                    QuantityOnHand = 500,
                    SourceDocument = "SEED"
                });
            }
        }

        if (!db.FiscalPeriods.Any())
        {
            var year = DateTime.Today.Year;
            for (var month = 1; month <= 12; month++)
            {
                var start = new DateTime(year, month, 1);
                db.FiscalPeriods.Add(new FiscalPeriod
                {
                    FiscalYear = year,
                    PeriodNumber = month,
                    PeriodName = start.ToString("MMM-yyyy"),
                    StartDate = start,
                    EndDate = start.AddMonths(1).AddDays(-1)
                });
            }
        }

        if (!db.Taxes.Any())
        {
            db.Taxes.Add(new Tax
            {
                TaxCode = "VAT-STD",
                TaxName = "Standard VAT",
                TaxType = TaxType.Percentage,
                Rate = 15,
                IsDefault = true
            });
        }

        if (!db.BomHeaders.Any())
        {
            var finished = db.Products.First(x => x.Code == "FG-001");
            var rawX = db.Products.First(x => x.Code == "RM-001");
            var packY = db.Products.First(x => x.Code == "RM-002");
            db.BomHeaders.Add(new BomHeader
            {
                ProductId = finished.Id,
                Version = "V1",
                Lines = new List<BomLine>
                {
                    new() { MaterialProductId = rawX.Id, QuantityRequired = 2.50m },
                    new() { MaterialProductId = packY.Id, QuantityRequired = 1.00m }
                }
            });
        }

        if (!db.ExpenseEntries.Any())
            db.ExpenseEntries.Add(new ExpenseEntry { ExpenseDate = DateTime.Today, ExpenseType = "Fuel", Amount = 8500, Description = "Route vehicle fuel" });

        if (!db.ProductionOrders.Any())
            db.ProductionOrders.Add(new ProductionOrder { OrderNo = "PROD-SEED-001", OrderDate = DateTime.Today, FinishedProductId = db.Products.First(x => x.Code == "FG-001").Id, PlannedQuantity = 100, ProducedQuantity = 100, MaterialCost = 5000, LaborCost = 1200, OverheadCost = 800, Status = "Completed" });

        if (!db.JournalEntries.Any())
        {
            db.JournalEntries.Add(new JournalEntry
            {
                EntryNo = "JV-OPEN-001",
                EntryDate = DateTime.Today,
                Description = "Opening capital",
                TotalDebit = 100000,
                TotalCredit = 100000,
                Lines = new List<JournalLine>
                {
                    new() { AccountCode = "1000", AccountName = "Cash", Debit = 100000, Credit = 0 },
                    new() { AccountCode = "3000", AccountName = "Capital", Debit = 0, Credit = 100000 }
                }
            });
        }

        await db.SaveChangesAsync();

        if (!db.SupplierInvoices.Any())
        {
            var supplier = db.Suppliers.First();
            db.SupplierInvoices.Add(new SupplierInvoice
            {
                InvoiceNo = "SINV-0001",
                InvoiceDate = DateTime.Today,
                SupplierId = supplier.Id,
                TotalAmount = 25000,
                PaidAmount = 5000,
                BalanceAmount = 20000,
                DueDate = DateTime.Today.AddDays(14),
                Status = "Open"
            });
        }

        if (!db.SupplierPayments.Any())
        {
            var supplier = db.Suppliers.First();
            db.SupplierPayments.Add(new SupplierPayment
            {
                PaymentNo = "SPAY-0001",
                PaymentDate = DateTime.Today,
                SupplierId = supplier.Id,
                ReferenceInvoiceNo = "SINV-0001",
                Amount = 5000,
                PaymentMethod = "Cash",
                Notes = "Opening sample supplier payment"
            });
        }

        if (!db.CollectionEntries.Any())
        {
            var customer = db.Customers.First();
            db.CollectionEntries.Add(new CollectionEntry
            {
                CustomerId = customer.Id,
                Amount = 5000,
                ReferenceNo = "RCPT-0001",
                Notes = "Opening sample collection",
                CollectionDate = DateTime.Now
            });
        }

        if (!db.SystemReports.Any())
            db.SystemReports.AddRange(
                new SystemReport { ReportCode = "RPT-SALES", ReportName = "Sales Register", ReportType = "FastReport-Ready", TemplatePath = "Reports/Templates/SalesRegister.frx" },
                new SystemReport { ReportCode = "RPT-PO", ReportName = "Purchase Order Register", ReportType = "FastReport-Ready", TemplatePath = "Reports/Templates/PurchaseOrders.frx" },
                new SystemReport { ReportCode = "RPT-INV", ReportName = "Invoice Print", ReportType = "FastReport-Ready", TemplatePath = "Reports/Templates/Invoice.frx" }
            );

        if (!db.AuditLogs.Any())
            db.AuditLogs.Add(new AuditLog
            {
                UserId = db.Users.First().Id,
                Action = "Seed",
                EntityName = "Database",
                EntityKey = "Init",
                NewValues = "Initial sample data created",
                ActionAtUtc = DateTime.UtcNow
            });

        if (!db.PurchaseOrders.Any())
        {
            var supplier = db.Suppliers.First();
            var rawMaterial = db.Products.First(x => x.Code == "RM-001");
            
            var orders = new List<PurchaseOrder>();
            for (int i = 0; i < 15; i++)
            {
                var orderDate = DateTime.Today.AddDays(-i * 12);
                orders.Add(new PurchaseOrder
                {
                    OrderNo = $"PO-SEED-{i:D3}",
                    OrderDate = orderDate,
                    SupplierId = supplier.Id,
                    Status = i < 3 ? "Pending" : "Completed",
                    TotalAmount = 15000 + (i * 500),
                    Items = new List<PurchaseOrderItem>
                    {
                        new() { ProductId = rawMaterial.Id, Quantity = 500, UnitPrice = 30 }
                    }
                });
            }
            db.PurchaseOrders.AddRange(orders);
        }

        if (!db.SalesInvoices.Any())
        {
            var customer = db.Customers.First();
            var vehicle = db.Vehicles.First();
            var product = db.Products.First(x => x.Code == "FG-001");
            
            var invoices = new List<SalesInvoice>();
            for (int i = 0; i < 30; i++)
            {
                var invoiceDate = DateTime.Today.AddDays(-i * 5);
                var qty = 10 + (i % 5) * 5;
                invoices.Add(new SalesInvoice
                {
                    InvoiceNo = $"INV-SEED-{i:D3}",
                    InvoiceDate = invoiceDate,
                    CustomerId = customer.Id,
                    VehicleId = vehicle.Id,
                    SaleType = SaleType.Cash,
                    TotalAmount = qty * product.SellingPrice,
                    PaidAmount = (i % 3 == 0) ? 0 : qty * product.SellingPrice,
                    Items = new List<SalesInvoiceItem>
                    {
                        new() { ProductId = product.Id, Quantity = qty, UnitPrice = product.SellingPrice }
                    }
                });
            }
            db.SalesInvoices.AddRange(invoices);
        }

        await db.SaveChangesAsync();
    }

    private static string GetInitialAdminPassword()
    {
        var configuredPassword = Environment.GetEnvironmentVariable("MANUFACTURINGERP_ADMIN_PASSWORD");
        if (!string.IsNullOrWhiteSpace(configuredPassword))
            return configuredPassword;

        return "admin123";
    }

    private static void WriteFirstRunPasswordFile(string password)
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MANUFACTURINGERP_ADMIN_PASSWORD")))
            return;

        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "first-run-admin.txt");
        if (File.Exists(path))
            return;

        File.WriteAllText(
            path,
            $"Initial admin login{Environment.NewLine}Username: {AppConstants.DefaultAdminUser}{Environment.NewLine}Password: {password}{Environment.NewLine}Delete this file after creating real user accounts.{Environment.NewLine}");
    }

    private static void EnsureFirstRunAdminPasswordMatchesFile(AppDbContext db, PasswordHasherService hasher)
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MANUFACTURINGERP_ADMIN_PASSWORD")))
            return;

        var password = TryReadFirstRunPasswordFile();
        if (string.IsNullOrWhiteSpace(password))
            return;

        var admin = db.Users.FirstOrDefault(x => x.Username == AppConstants.DefaultAdminUser);
        if (admin is null || hasher.Verify(password, admin.PasswordHash))
            return;

        admin.PasswordHash = hasher.Hash(password);
        admin.IsActive = true;
        admin.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static string? TryReadFirstRunPasswordFile()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "first-run-admin.txt");
        if (!File.Exists(path))
            return null;

        foreach (var line in File.ReadLines(path))
        {
            if (line.StartsWith("Password:", StringComparison.OrdinalIgnoreCase))
                return line.Split(':', 2)[1].Trim();
        }

        return null;
    }
}
