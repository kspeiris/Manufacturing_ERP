using ManufacturingERP.Application.Abstractions;
using ManufacturingERP.Application.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ManufacturingERP.Application.Services;

public class DashboardService
{
    private readonly IAppDbContext _db;
    private readonly AnalyticsService _analyticsService;

    public DashboardService(IAppDbContext db, AnalyticsService analyticsService)
    {
        _db = db;
        _analyticsService = analyticsService;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync()
    {
        var today = DateTime.Today;
        return new DashboardSummaryDto
        {
            TotalCustomers = await _db.Customers.CountAsync(),
            TotalProducts = await _db.Products.CountAsync(),
            TotalVehicles = await _db.Vehicles.CountAsync(),
            TodaySales = (decimal)(await _db.SalesInvoices
                .Where(x => x.InvoiceDate.Date == today)
                .SumAsync(x => (double?)x.TotalAmount) ?? 0),
            OutstandingBalance = (decimal)(await _db.Customers
                .SumAsync(x => (double?)x.OutstandingBalance) ?? 0),
            Analytics = await _analyticsService.GetAnalyticsAsync()
        };
    }
}
