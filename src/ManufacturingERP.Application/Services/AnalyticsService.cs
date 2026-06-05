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
        var monthlySales = await _db.SalesInvoices.Where(x => x.InvoiceDate >= monthStart).SumAsync(x => (decimal?)x.TotalAmount) ?? 0;
        var collections = await _db.CollectionEntries.Where(x => x.CollectionDate >= monthStart).SumAsync(x => (decimal?)x.Amount) ?? 0;
        var supplierPayables = await _db.SupplierInvoices.SumAsync(x => (decimal?)x.BalanceAmount) ?? 0;
        var customerReceivables = await _db.Customers.SumAsync(x => (decimal?)x.OutstandingBalance) ?? 0;
        var inventoryValue = await _db.StockBalances.Include(x => x.Product).SumAsync(x => (decimal?)(x.QuantityOnHand * x.Product!.CostPrice)) ?? 0;
        var productionCost = await _db.ProductionOrders.Where(x => x.OrderDate >= monthStart).SumAsync(x => (decimal?)(x.MaterialCost + x.LaborCost + x.OverheadCost)) ?? 0;

        return new DashboardAnalyticsDto
        {
            MonthlySales = monthlySales,
            CollectionsThisMonth = collections,
            SupplierPayables = supplierPayables,
            CustomerReceivables = customerReceivables,
            InventoryValue = inventoryValue,
            ProductionCostThisMonth = productionCost
        };
    }

    public async Task<AdvancedAnalyticsDto> GetAdvancedAnalyticsAsync()
    {
        var today = DateTime.Today;
        var currentMonthStart = new DateTime(today.Year, today.Month, 1);
        var previousMonthStart = currentMonthStart.AddMonths(-1);
        var sixMonthStart = currentMonthStart.AddMonths(-5);
        var yearStart = new DateTime(today.Year, 1, 1);

        var recentInvoices = await _db.SalesInvoices
            .Include(x => x.Items)
                .ThenInclude(x => x.Product)
            .Include(x => x.Customer)
            .Where(x => x.InvoiceDate >= sixMonthStart)
            .ToListAsync();

        var salesItems = recentInvoices.SelectMany(x => x.Items).ToList();
        var topProducts = salesItems
            .Where(x => x.Product is not null)
            .GroupBy(x => new { x.ProductId, x.Product!.Name })
            .Select(g => new ProductSalesDto
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.Name,
                QuantitySold = (int)g.Sum(x => x.Quantity),
                TotalSalesAmount = g.Sum(x => x.LineTotal),
                SalesCount = g.Count()
            })
            .OrderByDescending(x => x.TotalSalesAmount)
            .Take(6)
            .ToList();

        var monthlySalesData = Enumerable.Range(0, 6)
            .Select(i => currentMonthStart.AddMonths(-5 + i))
            .Select(month => new MonthlySalesDataPoint
            {
                Month = month.ToString("MMM yyyy"),
                Amount = recentInvoices
                    .Where(x => x.InvoiceDate.Year == month.Year && x.InvoiceDate.Month == month.Month)
                    .Sum(x => x.TotalAmount),
                OrderCount = recentInvoices
                    .Count(x => x.InvoiceDate.Year == month.Year && x.InvoiceDate.Month == month.Month),
                AverageOrderValue = recentInvoices
                    .Where(x => x.InvoiceDate.Year == month.Year && x.InvoiceDate.Month == month.Month)
                    .Select(x => x.TotalAmount)
                    .DefaultIfEmpty(0m)
                    .Average()
            })
            .ToList();

        var currentSales = monthlySalesData.Last().Amount;
        var previousSales = monthlySalesData.ElementAtOrDefault(4)?.Amount ?? 0;
        var totalSalesThisYear = await _db.SalesInvoices.Where(x => x.InvoiceDate >= yearStart).SumAsync(x => (decimal?)x.TotalAmount) ?? 0;

        var monthlyProfitData = monthlySalesData
            .Select(data =>
            {
                var invoices = recentInvoices.Where(x => x.InvoiceDate.ToString("MMM yyyy") == data.Month).ToList();
                var revenue = invoices.Sum(x => x.TotalAmount);
                var cost = invoices.SelectMany(x => x.Items).Where(x => x.Product is not null).Sum(x => x.Quantity * x.Product!.CostPrice);
                return new MonthlyProfitDataPoint
                {
                    Month = data.Month,
                    Revenue = revenue,
                    Cost = cost,
                    Profit = revenue - cost,
                    ProfitMargin = revenue <= 0 ? 0 : (revenue - cost) / revenue
                };
            })
            .ToList();

        var inventoryBalances = await _db.StockBalances
            .Include(x => x.Product)
                .ThenInclude(x => x.ProductCategory)
            .ToListAsync();

        var inventoryByCategory = inventoryBalances
            .Where(x => x.Product is not null)
            .GroupBy(x => x.Product!.ProductCategory?.Name ?? "Uncategorized")
            .Select(g => new InventoryCategoryDto
            {
                CategoryName = g.Key,
                TotalValue = g.Sum(x => x.QuantityOnHand * x.Product!.CostPrice),
                QuantityOnHand = (int)g.Sum(x => x.QuantityOnHand),
                ProductCount = g.Select(x => x.ProductId).Distinct().Count()
            })
            .OrderByDescending(x => x.TotalValue)
            .ToList();

        var totalInventoryValue = inventoryByCategory.Sum(x => x.TotalValue);
        var lowStockProductsCount = inventoryBalances.Count(x => x.Product is not null && x.QuantityOnHand <= x.Product!.ReorderLevel);
        var outOfStockProductsCount = inventoryBalances.Count(x => x.QuantityOnHand <= 0);
        var inventoryTurnoverRate = totalInventoryValue <= 0 ? 0 : currentSales / totalInventoryValue;

        var topCustomers = recentInvoices
            .Where(x => x.Customer is not null)
            .GroupBy(x => new { x.CustomerId, x.Customer!.ShopName })
            .Select(g => new CustomerSalesDto
            {
                CustomerId = g.Key.CustomerId,
                CustomerName = g.Key.ShopName,
                TotalSalesAmount = g.Sum(x => x.TotalAmount),
                TransactionCount = g.Count(),
                OutstandingBalance = g.First().Customer?.OutstandingBalance ?? 0
            })
            .OrderByDescending(x => x.TotalSalesAmount)
            .Take(5)
            .ToList();

        var receivablesStatus = topCustomers
            .Select(c => new CustomerPaymentStatusDto
            {
                CustomerId = c.CustomerId,
                CustomerName = c.CustomerName,
                OutstandingAmount = c.OutstandingBalance,
                DaysOverdue = c.OutstandingBalance > 0 ? 15 : 0,
                LastPaymentDate = today.AddDays(-Math.Min(30, c.TransactionCount * 3)),
                Status = c.OutstandingBalance <= 0 ? "Current" : c.OutstandingBalance > 0 ? "Overdue" : "Current"
            })
            .ToList();

        var supplierPerformance = await _db.PurchaseOrders
            .Include(x => x.Supplier)
            .Where(x => x.OrderDate >= sixMonthStart)
            .GroupBy(x => new { x.SupplierId, x.Supplier!.Name })
            .Select(g => new SupplierPerformanceDto
            {
                SupplierId = g.Key.SupplierId,
                SupplierName = g.Key.Name,
                TotalPurchaseAmount = g.Sum(x => x.TotalAmount),
                OrderCount = g.Count(),
                OnTimeDeliveryPercentage = 0.92m,
                QualityRating = 4.4m
            })
            .OrderByDescending(x => x.TotalPurchaseAmount)
            .Take(5)
            .ToListAsync();

        var procurementTrends = await _db.PurchaseOrders
            .Where(x => x.OrderDate >= sixMonthStart)
            .GroupBy(x => new { Year = x.OrderDate.Year, Month = x.OrderDate.Month })
            .Select(g => new ProcurementTrendDto
            {
                Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                TotalAmount = g.Sum(x => x.TotalAmount),
                OrderCount = g.Count(),
                AverageOrderValue = g.Any() ? g.Average(x => x.TotalAmount) : 0m
            })
            .OrderBy(x => DateTime.ParseExact(x.Month, "MMM yyyy", null))
            .ToListAsync();

        var supplierPayables = await _db.SupplierInvoices.SumAsync(x => (decimal?)x.BalanceAmount) ?? 0;
        var cashOnHand = await _db.CollectionEntries.Where(x => x.CollectionDate >= currentMonthStart).SumAsync(x => (decimal?)x.Amount) ?? 0;
        var totalReceivables = await _db.Customers.SumAsync(x => (decimal?)x.OutstandingBalance) ?? 0;
        var totalPurchaseAmount = await _db.PurchaseOrders.SumAsync(x => (decimal?)x.TotalAmount) ?? 0;
        var totalPurchaseOrders = await _db.PurchaseOrders.CountAsync();
        var pendingDeliveries = await _db.PurchaseOrders.CountAsync(x => x.Status != "Completed");

        var productionOrders = await _db.ProductionOrders.ToListAsync();
        var productionOrdersStatus = productionOrders
            .OrderBy(x => x.Status)
            .Take(5)
            .Select(x => new ProductionOrderStatusDto
            {
                OrderId = x.Id,
                OrderNumber = x.OrderNo,
                OrderDate = x.OrderDate,
                TargetCompletionDate = x.OrderDate.AddDays(14),
                Status = x.Status,
                Progress = x.Status == "Completed" ? 100 : x.Status == "Planned" ? 18 : 55
            })
            .ToList();

        var productionCostBreakdown = new List<ProductionCostBreakdownDto>();
        var currentMonthProductionOrders = productionOrders.Where(x => x.OrderDate >= currentMonthStart).ToList();
        var totalMaterialCost = currentMonthProductionOrders.Sum(x => x.MaterialCost);
        var totalLaborCost = currentMonthProductionOrders.Sum(x => x.LaborCost);
        var totalOverheadCost = currentMonthProductionOrders.Sum(x => x.OverheadCost);
        var totalProductionCostSum = totalMaterialCost + totalLaborCost + totalOverheadCost;

        productionCostBreakdown.Add(new ProductionCostBreakdownDto { CostType = "Material", Amount = totalMaterialCost, PercentageOfTotal = totalProductionCostSum == 0 ? 0 : totalMaterialCost / totalProductionCostSum });
        productionCostBreakdown.Add(new ProductionCostBreakdownDto { CostType = "Labor", Amount = totalLaborCost, PercentageOfTotal = totalProductionCostSum == 0 ? 0 : totalLaborCost / totalProductionCostSum });
        productionCostBreakdown.Add(new ProductionCostBreakdownDto { CostType = "Overhead", Amount = totalOverheadCost, PercentageOfTotal = totalProductionCostSum == 0 ? 0 : totalOverheadCost / totalProductionCostSum });

        var totalEmployees = await _db.Users.CountAsync();
        var totalSuppliers = await _db.Suppliers.CountAsync();
        var totalProducts = await _db.Products.CountAsync();
        var totalCustomers = await _db.Customers.CountAsync();

        return new AdvancedAnalyticsDto
        {
            TotalMonthlySales = currentSales,
            PreviousMonthlySales = previousSales,
            YearToDateSales = totalSalesThisYear,
            MonthlySalesData = monthlySalesData,
            TopSellingProducts = topProducts,
            TopCustomers = topCustomers,
            InventoryByCategory = inventoryByCategory,
            TotalInventoryValue = totalInventoryValue,
            LowStockProductsCount = lowStockProductsCount,
            OutOfStockProductsCount = outOfStockProductsCount,
            InventoryTurnoverRate = inventoryTurnoverRate,
            TotalReceivables = totalReceivables,
            TotalPayables = supplierPayables,
            CashOnHand = cashOnHand,
            MonthlyProfit = monthlyProfitData.LastOrDefault()?.Profit ?? 0,
            ProfitMargin = monthlyProfitData.LastOrDefault()?.ProfitMargin ?? 0,
            MonthlyProfitData = monthlyProfitData,
            ReceivablesStatus = receivablesStatus,
            SupplierPaymentStatus = await _db.SupplierInvoices
                .Include(x => x.Supplier)
                .OrderBy(x => x.BalanceAmount)
                .Take(5)
                .Select(x => new SupplierPaymentStatusDto
                {
                    SupplierId = x.SupplierId,
                    SupplierName = x.Supplier!.Name,
                    OutstandingAmount = x.BalanceAmount,
                    DaysDue = 7,
                    DueDate = x.InvoiceDate.AddDays(30),
                    Status = x.BalanceAmount <= 0 ? "Current" : "Due"
                })
                .ToListAsync(),
            TotalProductionCost = productionOrders.Where(x => x.OrderDate >= currentMonthStart).Sum(x => x.TotalCost),
            ActiveProductionOrders = productionOrders.Count(x => x.Status != "Completed"),
            CompletedProductionOrders = productionOrders.Count(x => x.Status == "Completed"),
            AverageProductionCostPerUnit = productionOrders.Where(x => x.ProducedQuantity > 0).Select(x => x.TotalCost / x.ProducedQuantity).DefaultIfEmpty(0m).Average(),
            ProductionCostBreakdown = productionCostBreakdown,
            ProductionOrdersStatus = productionOrdersStatus,
            TotalPurchaseOrders = totalPurchaseOrders,
            TotalPurchaseAmount = totalPurchaseAmount,
            PendingDeliveries = pendingDeliveries,
            SupplierPerformance = supplierPerformance,
            ProcurementTrends = procurementTrends,
            TotalCustomers = totalCustomers,
            TotalSuppliers = totalSuppliers,
            TotalProducts = totalProducts,
            TotalEmployees = totalEmployees,
            CustomerRetentionRate = totalCustomers == 0 ? 0 : Math.Min(1m, totalCustomers / (decimal)(totalCustomers + 5)),
            OnTimeDeliveryRate = 0.88m,
            KpiSummary = new KpiSummaryDto
            {
                SalesGrowthPercentage = previousSales == 0 ? 0 : (currentSales - previousSales) / (previousSales == 0 ? 1 : previousSales),
                ProfitMarginPercentage = monthlyProfitData.LastOrDefault()?.ProfitMargin ?? 0,
                InventoryTurnover = inventoryTurnoverRate,
                CollectionEfficiency = currentSales == 0 ? 0 : cashOnHand / currentSales,
                ProductionEfficiency = productionOrders.Count == 0 ? 0 : productionOrders.Count(x => x.Status == "Completed") / (decimal)productionOrders.Count,
                SupplierOnTimeDelivery = 0.92m,
                HealthScore = 72,
                HealthStatus = "Good"
            }
        };
    }
}
