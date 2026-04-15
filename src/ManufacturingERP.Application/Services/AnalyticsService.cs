using ManufacturingERP.Application.Abstractions;
using ManufacturingERP.Application.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ManufacturingERP.Application.Services;

public class AnalyticsService
{
    private readonly IAppDbContext _db;

    public AnalyticsService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardAnalyticsDto> GetAnalyticsAsync()
    {
        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var monthlySales = (await _db.SalesInvoices.Where(x => x.InvoiceDate >= monthStart).Select(x => x.TotalAmount).ToListAsync()).Sum();
        var collections = (await _db.CollectionEntries.Where(x => x.CollectionDate >= monthStart).Select(x => x.Amount).ToListAsync()).Sum();
        var supplierPayables = (await _db.SupplierInvoices.Select(x => x.BalanceAmount).ToListAsync()).Sum();
        var customerReceivables = (await _db.Customers.Select(x => x.OutstandingBalance).ToListAsync()).Sum();
        var inventoryValue = (await _db.StockBalances.Include(x => x.Product).Select(x => new { x.QuantityOnHand, CostPrice = x.Product!.CostPrice }).ToListAsync()).Sum(x => x.QuantityOnHand * x.CostPrice);
        var productionCost = (await _db.ProductionOrders.Where(x => x.OrderDate >= monthStart).Select(x => new { x.MaterialCost, x.LaborCost, x.OverheadCost }).ToListAsync()).Sum(x => x.MaterialCost + x.LaborCost + x.OverheadCost);
        return new DashboardAnalyticsDto { MonthlySales = monthlySales, CollectionsThisMonth = collections, SupplierPayables = supplierPayables, CustomerReceivables = customerReceivables, InventoryValue = inventoryValue, ProductionCostThisMonth = productionCost };
    }
}
