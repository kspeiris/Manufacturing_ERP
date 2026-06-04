using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Application.Services;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Domain.Enums;
using ManufacturingERP.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ManufacturingERP.Tests;

public class SecurityAuthorizationTests
{
    [Fact]
    public void AuthorizationMatrix_Should_Enforce_RoleRestrictions()
    {
        var salesAuth = CreateAuthorizationService(UserRole.Sales);
        var warehouseAuth = CreateAuthorizationService(UserRole.Warehouse);
        var accountsAuth = CreateAuthorizationService(UserRole.Accounts);
        var adminAuth = CreateAuthorizationService(UserRole.Admin);

        var salesAdminAccess = salesAuth.EnsureAdminAccess();
        var warehouseAccountingPost = warehouseAuth.EnsureAccountingPostAccess();
        var accountsLedgers = accountsAuth.EnsureLedgersAccess();
        var adminAccounting = adminAuth.EnsureAccountingPostAccess();
        var adminProduction = adminAuth.EnsureProductionPostAccess();

        Assert.False(salesAdminAccess.IsSuccess);
        Assert.Contains("access denied", salesAdminAccess.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(warehouseAccountingPost.IsSuccess);
        Assert.Contains("accounting", warehouseAccountingPost.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(accountsLedgers.IsSuccess, accountsLedgers.Message);
        Assert.True(adminAccounting.IsSuccess, adminAccounting.Message);
        Assert.True(adminProduction.IsSuccess, adminProduction.Message);
    }

    [Fact]
    public async Task EnsureSignedIn_Should_Block_Disabled_And_TimedOut_Sessions()
    {
        var disabledCurrentUser = new CurrentUserService();
        disabledCurrentUser.Set(new User { Id = 1, Username = "disabled", FullName = "Disabled", Role = UserRole.Sales, IsActive = false });
        var disabledAuth = new AuthorizationService(disabledCurrentUser);

        var disabledResult = disabledAuth.EnsureSignedIn();
        Assert.False(disabledResult.IsSuccess);
        Assert.Contains("disabled", disabledResult.Message, StringComparison.OrdinalIgnoreCase);

        var timedOutCurrentUser = new CurrentUserService();
        timedOutCurrentUser.Set(new User { Id = 2, Username = "sales", FullName = "Sales", Role = UserRole.Sales, IsActive = true }, TimeSpan.FromMilliseconds(10));
        await Task.Delay(30);

        var timedOutResult = new AuthorizationService(timedOutCurrentUser).EnsureSignedIn();
        Assert.False(timedOutResult.IsSuccess);
        Assert.Contains("timed out", timedOutResult.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unauthorized_Service_Actions_Should_Return_Clear_Messages()
    {
        var tempDirectory = CreateTempDirectory();
        var databasePath = Path.Combine(tempDirectory, "security-unauthorized.db");

        try
        {
            await using var db = CreateDb(databasePath);
            var accountsUser = new User { Username = "accounts", PasswordHash = "hash", FullName = "Accounts", Role = UserRole.Accounts, IsActive = true };
            var warehouseUser = new User { Username = "warehouse", PasswordHash = "hash", FullName = "Warehouse", Role = UserRole.Warehouse, IsActive = true };
            db.Users.AddRange(accountsUser, warehouseUser);
            db.Accounts.AddRange(
                new Account { AccountCode = "1000", AccountName = "Cash", AccountType = "Asset", IsActive = true },
                new Account { AccountCode = "3000", AccountName = "Capital", AccountType = "Equity", IsActive = true });
            await db.SaveChangesAsync();

            var warehouseCurrentUser = new CurrentUserService();
            warehouseCurrentUser.Set(warehouseUser);
            var accountingService = new AccountingService(db, new AuthorizationService(warehouseCurrentUser), new AuditService(db), warehouseCurrentUser);

            var accountingResult = await accountingService.CreateJournalEntryAsync(new CreateJournalEntryRequest
            {
                Description = "Unauthorized journal",
                Lines =
                [
                    new() { AccountCode = "1000", Debit = 10m },
                    new() { AccountCode = "3000", Credit = 10m }
                ]
            });

            Assert.False(accountingResult.IsSuccess);
            Assert.Contains("access denied", accountingResult.Message, StringComparison.OrdinalIgnoreCase);

            var accountsCurrentUser = new CurrentUserService();
            accountsCurrentUser.Set(accountsUser);
            var userManagementService = new UserManagementService(
                db,
                new AuthorizationService(accountsCurrentUser),
                new AuditService(db),
                accountsCurrentUser,
                new PasswordHasherService());

            var userSaveResult = await userManagementService.SaveUserAsync(new UserCrudRequest
            {
                Username = "new-user",
                Password = "1234",
                FullName = "New User",
                Role = UserRole.Sales,
                IsActive = true
            });

            Assert.False(userSaveResult.IsSuccess);
            Assert.Contains("access denied", userSaveResult.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempDirectory(tempDirectory);
        }
    }

    [Fact]
    public async Task Authorized_Transactions_Should_Write_Audit_Logs()
    {
        var tempDirectory = CreateTempDirectory();
        var databasePath = Path.Combine(tempDirectory, "security-audit.db");

        try
        {
            await using var db = CreateDb(databasePath);
            var admin = new User { Username = "admin", PasswordHash = "hash", FullName = "Admin", Role = UserRole.Admin, IsActive = true };
            var customer = new Customer { CustomerCode = "C-AUD", ShopName = "Audit Customer", OwnerName = "Owner", Route = "Route", CreditLimit = 1000m, OutstandingBalance = 0m, IsActive = true };
            var vehicle = new Vehicle { VehicleNumber = "VEH-AUD", Description = "Audit Van", IsActive = true };
            var warehouse = new Warehouse { Name = "Main", Location = "HQ", IsActive = true };
            var category = new ProductCategory { Name = "FG" };
            var product = new Product { Code = "P-AUD", Name = "Audit Product", ProductCategory = category, Unit = "PCS", CostPrice = 10m, SellingPrice = 25m, IsActive = true };

            db.Users.Add(admin);
            db.Customers.Add(customer);
            db.Vehicles.Add(vehicle);
            db.Warehouses.Add(warehouse);
            db.ProductCategories.Add(category);
            db.Products.Add(product);
            await db.SaveChangesAsync();

            db.StockBalances.Add(new StockBalance { ProductId = product.Id, WarehouseId = warehouse.Id, QuantityOnHand = 5m });
            db.Accounts.AddRange(
                new Account { AccountCode = "1000", AccountName = "Cash", AccountType = "Asset", IsActive = true },
                new Account { AccountCode = "3000", AccountName = "Capital", AccountType = "Equity", IsActive = true });
            db.FiscalPeriods.Add(new FiscalPeriod
            {
                FiscalYear = DateTime.Today.Year,
                PeriodNumber = DateTime.Today.Month,
                PeriodName = "Test Period",
                StartDate = DateTime.Today.AddDays(-30),
                EndDate = DateTime.Today.AddDays(30)
            });
            await db.SaveChangesAsync();

            var currentUserService = new CurrentUserService();
            currentUserService.Set(admin);
            var authorizationService = new AuthorizationService(currentUserService);
            var auditService = new AuditService(db);

            var salesService = new SalesService(db, authorizationService, auditService, currentUserService);
            var warehouseService = new WarehouseService(db, authorizationService, auditService, currentUserService);
            var accountingService = new AccountingService(db, authorizationService, auditService, currentUserService);

            var invoiceResult = await salesService.CreateInvoiceAsync(new CreateInvoiceRequest
            {
                CustomerId = customer.Id,
                VehicleId = vehicle.Id,
                SaleType = SaleType.Cash,
                PaidAmount = 25m,
                Items = [new CreateInvoiceLineRequest { ProductId = product.Id, Quantity = 1m, UnitPrice = 25m }]
            });
            Assert.True(invoiceResult.IsSuccess, invoiceResult.Message);

            var adjustmentResult = await warehouseService.CreateAdjustmentAsync(product.Id, warehouse.Id, 2m, "Audit adjustment");
            Assert.True(adjustmentResult.IsSuccess, adjustmentResult.Message);

            var journalResult = await accountingService.CreateJournalEntryAsync(new CreateJournalEntryRequest
            {
                Description = "Manual audit journal",
                Lines =
                [
                    new() { AccountCode = "1000", Debit = 50m },
                    new() { AccountCode = "3000", Credit = 50m }
                ]
            });
            Assert.True(journalResult.IsSuccess, journalResult.Message);

            var logs = await db.AuditLogs.OrderBy(x => x.Id).ToListAsync();
            Assert.Contains(logs, x => x.EntityName == "SalesInvoice" && x.Action == "Create");
            Assert.Contains(logs, x => x.EntityName == "WarehouseAdjustment" && x.Action == "Create");
            Assert.Contains(logs, x => x.EntityName == "JournalEntry" && x.Action == "Create");
        }
        finally
        {
            CleanupTempDirectory(tempDirectory);
        }
    }

    private static AuthorizationService CreateAuthorizationService(UserRole role)
    {
        var currentUserService = new CurrentUserService();
        currentUserService.Set(new User
        {
            Id = (int)role,
            Username = role.ToString().ToLowerInvariant(),
            FullName = role.ToString(),
            Role = role,
            IsActive = true
        });
        return new AuthorizationService(currentUserService);
    }

    private static AppDbContext CreateDb(string databasePath)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .Options;
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static string CreateTempDirectory()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "ManufacturingERP.SecurityTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        return tempDirectory;
    }

    private static void CleanupTempDirectory(string tempDirectory)
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(tempDirectory))
            Directory.Delete(tempDirectory, true);
    }
}
