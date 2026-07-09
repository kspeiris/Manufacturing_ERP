using System;
using System.Linq;
using System.Threading.Tasks;
using ManufacturingERP.Infrastructure.Persistence;
using ManufacturingERP.Application.Services;
using Microsoft.EntityFrameworkCore;

class Program
{
    static async Task Main()
    {
        try
        {
            var dbPath = @"c:\Projects\ManufacturingERP\src\ManufacturingERP.Desktop\bin\Debug\net8.0-windows\manufacturing_erp.db";
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={dbPath}").Options;
            using var db = new AppDbContext(options);
            
            var analyticsService = new AnalyticsService(db);
            var analytics = await analyticsService.GetAdvancedAnalyticsAsync();
            Console.WriteLine("SUCCESS!");
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR: " + ex.Message);
            Console.WriteLine(ex.StackTrace);
        }
    }
}
