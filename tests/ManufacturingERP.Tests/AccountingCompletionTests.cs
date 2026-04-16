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

public class AccountingCompletionTests
{
    [Fact]
    public async Task JournalEntry_Should_Save_And_RequireBalancedTotals()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "ManufacturingERP.AccountingTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var databasePath = Path.Combine(tempDirectory, "accounting.db");

        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={databasePath}").Options;
            await using var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();
            db.Accounts.AddRange(
                new Account { AccountCode = "1000", AccountName = "Cash", AccountType = "Asset", IsActive = true },
                new Account { AccountCode = "3000", AccountName = "Capital", AccountType = "Equity", IsActive = true }
            );
            var user = new User
            {
                Username = AppConstants.DefaultAdminUser,
                PasswordHash = "hash",
                FullName = "Admin User",
                Role = UserRole.Admin,
                IsActive = true
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var currentUserService = new CurrentUserService();
            currentUserService.Set(user);
            var service = new AccountingService(db, new AuthorizationService(currentUserService), new AuditService(db), currentUserService);
            var invalid = await service.CreateJournalEntryAsync(new CreateJournalEntryRequest { Description = "Bad journal", Lines = [ new() { AccountCode = "1000", Debit = 100m }, new() { AccountCode = "3000", Credit = 90m } ] });
            Assert.False(invalid.IsSuccess);

            var valid = await service.CreateJournalEntryAsync(new CreateJournalEntryRequest { Description = "Capital intro", Lines = [ new() { AccountCode = "1000", Debit = 100m }, new() { AccountCode = "3000", Credit = 100m } ] });
            Assert.True(valid.IsSuccess, valid.Message);

            var trial = await service.GetTrialBalanceAsync();
            Assert.Equal(trial.Sum(x => x.Debit), trial.Sum(x => x.Credit));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
        }
    }
}
