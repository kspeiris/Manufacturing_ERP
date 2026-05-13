using ManufacturingERP.Infrastructure.Persistence;
using ManufacturingERP.Shared.Constants;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ManufacturingERP.Tests;

public class DatabaseInitializationTests
{
    [Fact]
    public async Task Migrate_And_Seed_Should_Create_DefaultData()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "ManufacturingERP.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var databasePath = Path.Combine(tempDirectory, AppConstants.DatabaseFileName);

        try
        {
            Environment.SetEnvironmentVariable("MANUFACTURINGERP_ADMIN_PASSWORD", "Test-Admin-Password-123!");
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;

            await using var db = new AppDbContext(options);
            await db.Database.MigrateAsync();
            await DbSeeder.SeedAsync(db);

            Assert.True(File.Exists(databasePath));
            Assert.Equal(1, await db.Users.CountAsync(x => x.Username == AppConstants.DefaultAdminUser));
            Assert.True(await db.Users.CountAsync() >= 1);
            Assert.True(await db.Products.CountAsync() >= 4);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MANUFACTURINGERP_ADMIN_PASSWORD", null);
            SqliteConnection.ClearAllPools();

            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, true);
        }
    }
}
