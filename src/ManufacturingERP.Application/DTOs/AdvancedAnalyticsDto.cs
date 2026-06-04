namespace ManufacturingERP.Application.DTOs;

/// <summary>
/// Comprehensive analytics data for dashboard and reports
/// </summary>
public class AdvancedAnalyticsDto
{
    // Sales Metrics
    public decimal TotalMonthlySales { get; set; }
    public decimal PreviousMonthlySales { get; set; }
    public decimal YearToDateSales { get; set; }
    public List<MonthlySalesDataPoint> MonthlySalesData { get; set; } = new();
    public List<ProductSalesDto> TopSellingProducts { get; set; } = new();
    public List<CustomerSalesDto> TopCustomers { get; set; } = new();

    // Inventory Metrics
    public decimal TotalInventoryValue { get; set; }
    public int LowStockProductsCount { get; set; }
    public int OutOfStockProductsCount { get; set; }
    public List<InventoryCategoryDto> InventoryByCategory { get; set; } = new();
    public List<StockMovementDto> RecentStockMovements { get; set; } = new();
    public decimal InventoryTurnoverRate { get; set; }

    // Financial Metrics
    public decimal TotalReceivables { get; set; }
    public decimal TotalPayables { get; set; }
    public decimal CashOnHand { get; set; }
    public decimal MonthlyProfit { get; set; }
    public decimal ProfitMargin { get; set; }
    public List<MonthlyProfitDataPoint> MonthlyProfitData { get; set; } = new();
    public List<CustomerPaymentStatusDto> ReceivablesStatus { get; set; } = new();
    public List<SupplierPaymentStatusDto> PayablesStatus { get; set; } = new();
    public List<SupplierPaymentStatusDto> SupplierPaymentStatus { get; set; } = new();

    // Production Metrics
    public decimal TotalProductionCost { get; set; }
    public int ActiveProductionOrders { get; set; }
    public int CompletedProductionOrders { get; set; }
    public decimal AverageProductionCostPerUnit { get; set; }
    public List<ProductionCostBreakdownDto> ProductionCostBreakdown { get; set; } = new();
    public List<ProductionOrderStatusDto> ProductionOrdersStatus { get; set; } = new();

    // Procurement Metrics
    public int TotalPurchaseOrders { get; set; }
    public decimal TotalPurchaseAmount { get; set; }
    public int PendingDeliveries { get; set; }
    public List<SupplierPerformanceDto> SupplierPerformance { get; set; } = new();
    public List<ProcurementTrendDto> ProcurementTrends { get; set; } = new();

    // Operational Metrics
    public int TotalCustomers { get; set; }
    public int TotalSuppliers { get; set; }
    public int TotalProducts { get; set; }
    public int TotalEmployees { get; set; }
    public decimal CustomerRetentionRate { get; set; }
    public decimal OnTimeDeliveryRate { get; set; }

    // KPI Summary
    public KpiSummaryDto KpiSummary { get; set; } = new();
}

public class MonthlySalesDataPoint
{
    public string Month { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int OrderCount { get; set; }
    public decimal AverageOrderValue { get; set; }
}

public class MonthlyProfitDataPoint
{
    public string Month { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal Profit { get; set; }
    public decimal ProfitMargin { get; set; }
}

public class ProductSalesDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal TotalSalesAmount { get; set; }
    public int QuantitySold { get; set; }
    public int SalesCount { get; set; }
}

public class CustomerSalesDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal TotalSalesAmount { get; set; }
    public int TransactionCount { get; set; }
    public decimal OutstandingBalance { get; set; }
}

public class InventoryCategoryDto
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal TotalValue { get; set; }
    public int QuantityOnHand { get; set; }
    public int ProductCount { get; set; }
    public decimal PercentageOfTotal { get; set; }
}

public class StockMovementDto
{
    public DateTime MovementDate { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string MovementType { get; set; } = string.Empty; // In/Out/Adjustment
    public string Reference { get; set; } = string.Empty;
}

public class CustomerPaymentStatusDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal OutstandingAmount { get; set; }
    public int DaysOverdue { get; set; }
    public DateTime LastPaymentDate { get; set; }
    public string Status { get; set; } = string.Empty; // Current, Overdue, Defaulted
}

public class SupplierPaymentStatusDto
{
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public decimal OutstandingAmount { get; set; }
    public int DaysDue { get; set; }
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = string.Empty; // Current, Due, Overdue
}

public class ProductionCostBreakdownDto
{
    public string CostType { get; set; } = string.Empty; // Material, Labor, Overhead
    public decimal Amount { get; set; }
    public decimal PercentageOfTotal { get; set; }
}

public class ProductionOrderStatusDto
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime TargetCompletionDate { get; set; }
    public string Status { get; set; } = string.Empty; // Pending, In Progress, Completed
    public decimal Progress { get; set; } // 0-100
}

public class SupplierPerformanceDto
{
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public decimal TotalPurchaseAmount { get; set; }
    public int OrderCount { get; set; }
    public decimal OnTimeDeliveryPercentage { get; set; }
    public decimal QualityRating { get; set; }
}

public class ProcurementTrendDto
{
    public string Month { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int OrderCount { get; set; }
    public decimal AverageOrderValue { get; set; }
}

public class KpiSummaryDto
{
    public decimal SalesGrowthPercentage { get; set; }
    public decimal ProfitMarginPercentage { get; set; }
    public decimal InventoryTurnover { get; set; }
    public decimal CollectionEfficiency { get; set; }
    public decimal ProductionEfficiency { get; set; }
    public decimal SupplierOnTimeDelivery { get; set; }
    public int HealthScore { get; set; } // 0-100
    public string HealthStatus { get; set; } = string.Empty; // Excellent, Good, Fair, Poor
}
