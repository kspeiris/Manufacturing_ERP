namespace ManufacturingERP.Application.DTOs;

public class ProfitLossRowDto
{
    public string AccountType { get; set; } = string.Empty;
    public decimal Balance { get; set; }
}

public class BalanceSheetRowDto
{
    public string Section { get; set; } = string.Empty;
    public decimal Balance { get; set; }
}

public class DashboardAnalyticsDto
{
    public decimal MonthlySales { get; set; }
    public decimal CollectionsThisMonth { get; set; }
    public decimal SupplierPayables { get; set; }
    public decimal CustomerReceivables { get; set; }
    public decimal InventoryValue { get; set; }
    public decimal ProductionCostThisMonth { get; set; }
}
