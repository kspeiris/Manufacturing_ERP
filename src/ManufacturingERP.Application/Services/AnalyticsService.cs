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

        // SQLite cannot aggregate decimal directly — cast to double?, then back to decimal
        var monthlySales    = (decimal)(await _db.SalesInvoices.Where(x => x.InvoiceDate >= monthStart).SumAsync(x => (double?)x.TotalAmount) ?? 0);
        var collections     = (decimal)(await _db.CollectionEntries.Where(x => x.CollectionDate >= monthStart).SumAsync(x => (double?)x.Amount) ?? 0);
        var supplierPayables= (decimal)(await _db.SupplierInvoices.SumAsync(x => (double?)x.BalanceAmount) ?? 0);
        var customerReceivables = (decimal)(await _db.Customers.SumAsync(x => (double?)x.OutstandingBalance) ?? 0);
        var productionCost  = (decimal)(await _db.ProductionOrders.Where(x => x.OrderDate >= monthStart).SumAsync(x => (double?)(x.MaterialCost + x.LaborCost + x.OverheadCost)) ?? 0);

        // Inventory value: load to memory then sum (product navigation required)
        var stockBalances = await _db.StockBalances.Include(x => x.Product).ToListAsync();
        var inventoryValue = stockBalances.Where(x => x.Product != null).Sum(x => x.QuantityOnHand * x.Product!.CostPrice);

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
        var sixMonthStart = currentMonthStart.AddMonths(-5);
        var yearStart = new DateTime(today.Year, 1, 1);

        // --- Sales & Profit (all in-memory after loading with includes) ---
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

        // Year-to-date sales via double cast for SQLite
        var totalSalesThisYear = (decimal)(await _db.SalesInvoices
            .Where(x => x.InvoiceDate >= yearStart)
            .SumAsync(x => (double?)x.TotalAmount) ?? 0);

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

        // --- Inventory (in-memory, needs navigation) ---
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

        // --- Customers ---
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
                Status = c.OutstandingBalance <= 0 ? "Current" : "Overdue"
            })
            .ToList();

        // --- Supplier performance (in-memory grouping) ---
        var supplierOrdersRaw = await _db.PurchaseOrders
            .Include(x => x.Supplier)
            .Where(x => x.OrderDate >= sixMonthStart)
            .Select(x => new { x.SupplierId, SupplierName = x.Supplier!.Name, x.TotalAmount })
            .ToListAsync();

        var supplierPerformance = supplierOrdersRaw
            .GroupBy(x => new { x.SupplierId, x.SupplierName })
            .Select(g => new SupplierPerformanceDto
            {
                SupplierId = g.Key.SupplierId,
                SupplierName = g.Key.SupplierName,
                TotalPurchaseAmount = g.Sum(x => x.TotalAmount),
                OrderCount = g.Count(),
                OnTimeDeliveryPercentage = 0.92m,
                QualityRating = 4.4m
            })
            .OrderByDescending(x => x.TotalPurchaseAmount)
            .Take(5)
            .ToList();

        // --- Procurement trends (in-memory sort/format, no DateTime.ParseExact in EF) ---
        var procurementTrendsRaw = await _db.PurchaseOrders
            .Where(x => x.OrderDate >= sixMonthStart)
            .GroupBy(x => new { Year = x.OrderDate.Year, Month = x.OrderDate.Month })
            .Select(g => new
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                TotalAmount = g.Sum(x => (double)x.TotalAmount),
                OrderCount = g.Count(),
                AverageOrderValue = g.Count() > 0 ? g.Average(x => (double)x.TotalAmount) : 0.0
            })
            .ToListAsync();

        var procurementTrends = procurementTrendsRaw
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .Select(x => new ProcurementTrendDto
            {
                Month = new DateTime(x.Year, x.Month, 1).ToString("MMM yyyy"),
                TotalAmount = (decimal)x.TotalAmount,
                OrderCount = x.OrderCount,
                AverageOrderValue = (decimal)x.AverageOrderValue
            })
            .ToList();

        // --- Scalar aggregates (all via double cast for SQLite) ---
        var supplierPayables   = (decimal)(await _db.SupplierInvoices.SumAsync(x => (double?)x.BalanceAmount) ?? 0);
        var cashOnHand         = (decimal)(await _db.CollectionEntries.Where(x => x.CollectionDate >= currentMonthStart).SumAsync(x => (double?)x.Amount) ?? 0);
        var totalReceivables   = (decimal)(await _db.Customers.SumAsync(x => (double?)x.OutstandingBalance) ?? 0);
        var totalPurchaseAmount= (decimal)(await _db.PurchaseOrders.SumAsync(x => (double?)x.TotalAmount) ?? 0);
        var totalPurchaseOrders= await _db.PurchaseOrders.CountAsync();
        var pendingDeliveries  = await _db.PurchaseOrders.CountAsync(x => x.Status != "Completed");

        // --- Production (in-memory after ToListAsync) ---
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

        var currentMonthProductionOrders = productionOrders.Where(x => x.OrderDate >= currentMonthStart).ToList();
        var totalMaterialCost = currentMonthProductionOrders.Sum(x => x.MaterialCost);
        var totalLaborCost    = currentMonthProductionOrders.Sum(x => x.LaborCost);
        var totalOverheadCost = currentMonthProductionOrders.Sum(x => x.OverheadCost);
        var totalProductionCostSum = totalMaterialCost + totalLaborCost + totalOverheadCost;

        var productionCostBreakdown = new List<ProductionCostBreakdownDto>
        {
            new() { CostType = "Material", Amount = totalMaterialCost, PercentageOfTotal = totalProductionCostSum == 0 ? 0 : totalMaterialCost / totalProductionCostSum },
            new() { CostType = "Labor",    Amount = totalLaborCost,    PercentageOfTotal = totalProductionCostSum == 0 ? 0 : totalLaborCost    / totalProductionCostSum },
            new() { CostType = "Overhead", Amount = totalOverheadCost, PercentageOfTotal = totalProductionCostSum == 0 ? 0 : totalOverheadCost / totalProductionCostSum }
        };

        // --- Supplier payment status (in-memory after select) ---
        var supplierInvoices = await _db.SupplierInvoices
            .Include(x => x.Supplier)
            .OrderBy(x => (double)x.BalanceAmount)
            .Take(5)
            .ToListAsync();

        var supplierPaymentStatus = supplierInvoices
            .Select(x => new SupplierPaymentStatusDto
            {
                SupplierId = x.SupplierId,
                SupplierName = x.Supplier!.Name,
                OutstandingAmount = x.BalanceAmount,
                DaysDue = 7,
                DueDate = x.InvoiceDate.AddDays(30),
                Status = x.BalanceAmount <= 0 ? "Current" : "Due"
            })
            .ToList();

        var totalEmployees = await _db.Users.CountAsync();
        var totalSuppliers = await _db.Suppliers.CountAsync();
        var totalProducts  = await _db.Products.CountAsync();
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
            SupplierPaymentStatus = supplierPaymentStatus,
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
                SalesGrowthPercentage = previousSales == 0 ? 0 : (currentSales - previousSales) / previousSales,
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
