using ManufacturingERP.Application.Services;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ManufacturingERP.Tests;

public class DatabaseBackupServiceTests
{
    [Fact]
    public async Task CreateBackup_Validate_And_Restore_Should_CopyValidSqliteDatabase()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "ManufacturingERP.BackupTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var databasePath = Path.Combine(tempDirectory, "manufacturing_erp.db");
        var backupDirectory = Path.Combine(tempDirectory, "Backups");

        try
        {
            await using (var db = CreateDb(databasePath))
            {
                db.Users.Add(new User { Username = "admin", PasswordHash = "hash", FullName = "Admin", IsActive = true });
                await db.SaveChangesAsync();
            }

            var service = new DatabaseBackupService(databasePath);
            var backup = await service.CreateBackupAsync(backupDirectory);

            Assert.True(backup.IsSuccess, backup.Message);
            Assert.True(File.Exists(backup.Value));

            var validation = await service.ValidateBackupAsync(backup.Value);
            Assert.True(validation.IsSuccess, validation.Message);

            await using (var db = CreateDb(databasePath))
            {
                db.Users.Add(new User { Username = "operator", PasswordHash = "hash", FullName = "Operator", IsActive = true });
                await db.SaveChangesAsync();
            }

            var restore = await service.RestoreBackupAsync(backup.Value);
            Assert.True(restore.IsSuccess, restore.Message);

            await using (var db = CreateDb(databasePath))
            {
                var users = await db.Users.OrderBy(x => x.Username).Select(x => x.Username).ToListAsync();
                Assert.Equal(["admin"], users);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, true);
        }
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
}
