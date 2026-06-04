namespace ManufacturingERP.Application.DTOs;

public class DashboardSummaryDto
{
    public int TotalCustomers { get; set; }
    public int TotalProducts { get; set; }
    public int TotalVehicles { get; set; }
    public decimal TodaySales { get; set; }
    public decimal OutstandingBalance { get; set; }
    public DashboardAnalyticsDto Analytics { get; set; } = new();
    public AdvancedAnalyticsDto AdvancedAnalytics { get; set; } = new();
}
