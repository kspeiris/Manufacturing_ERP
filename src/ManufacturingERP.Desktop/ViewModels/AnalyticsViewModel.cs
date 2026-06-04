using CommunityToolkit.Mvvm.ComponentModel;
using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Application.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class AnalyticsViewModel : ViewModelBase
{
    private readonly AnalyticsService _analyticsService;

    [ObservableProperty] private AdvancedAnalyticsDto _analytics = new();
    [ObservableProperty] private ISeries[] _salesTrendSeries = Array.Empty<ISeries>();
    [ObservableProperty] private ISeries[] _profitTrendSeries = Array.Empty<ISeries>();
    [ObservableProperty] private ISeries[] _inventorySeries = Array.Empty<ISeries>();
    [ObservableProperty] private string[] _salesTrendLabels = Array.Empty<string>();
    [ObservableProperty] private string[] _profitTrendLabels = Array.Empty<string>();
    [ObservableProperty] private List<AnalyticsKpiCard> _kpiCards = new();

    public Func<double, string> CurrencyLabeler { get; } = value => value.ToString("C0");
    public Func<double, string> PercentLabeler { get; } = value => $"{value:P0}";

    public AnalyticsViewModel(AnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        Analytics = await _analyticsService.GetAdvancedAnalyticsAsync();
        BuildKpiCards();
        BuildCharts();
    }

    private void BuildKpiCards()
    {
        KpiCards = new List<AnalyticsKpiCard>
        {
            new("Monthly sales", Analytics.TotalMonthlySales, Analytics.KpiSummary.SalesGrowthPercentage, "Strong momentum for revenue growth."),
            new("Gross profit", Analytics.MonthlyProfit, Analytics.KpiSummary.ProfitMarginPercentage, "Margin performance across the current period."),
            new("Inventory value", Analytics.TotalInventoryValue, Analytics.KpiSummary.InventoryTurnover, "Stock investment and turnover velocity."),
            new("Receivables vs payables", Analytics.TotalReceivables - Analytics.TotalPayables, Analytics.KpiSummary.CollectionEfficiency, "Cash flow balance and working capital health.")
        };
    }

    private void BuildCharts()
    {
        SalesTrendLabels = Analytics.MonthlySalesData.Select(x => x.Month).ToArray();

        SalesTrendSeries = new ISeries[]
        {
            new LineSeries<decimal>
            {
                Name = "Sales",
                Values = Analytics.MonthlySalesData.Select(x => x.Amount).ToArray(),
                Fill = null,
                GeometrySize = 8,
                Stroke = new SolidColorPaint(SKColor.Parse("#2563EB"), 3)
            }
        };

        ProfitTrendLabels = Analytics.MonthlyProfitData.Select(x => x.Month).ToArray();
        ProfitTrendSeries = new ISeries[]
        {
            new LineSeries<decimal>
            {
                Name = "Profit",
                Values = Analytics.MonthlyProfitData.Select(x => x.Profit).ToArray(),
                Fill = null,
                GeometrySize = 8,
                Stroke = new SolidColorPaint(SKColor.Parse("#16A34A"), 3)
            },
            new LineSeries<decimal>
            {
                Name = "Cost",
                Values = Analytics.MonthlyProfitData.Select(x => x.Cost).ToArray(),
                Fill = null,
                GeometrySize = 8,
                Stroke = new SolidColorPaint(SKColor.Parse("#F97316"), 3)
            }
        };

        InventorySeries = Analytics.InventoryByCategory
            .OrderByDescending(x => x.TotalValue)
            .Take(6)
            .Select(x => new PieSeries<decimal>
            {
                Name = x.CategoryName,
                Values = new[] { x.TotalValue },
                Fill = new SolidColorPaint(SKColor.Parse("#" + Guid.NewGuid().ToString("N").Substring(0, 6)))
            })
            .Cast<ISeries>()
            .ToArray();
    }
}

public sealed class AnalyticsKpiCard
{
    public AnalyticsKpiCard(string title, decimal value, decimal change, string description)
    {
        Title = title;
        Value = value;
        Change = change;
        Description = description;
    }

    public string Title { get; }
    public decimal Value { get; }
    public decimal Change { get; }
    public string Description { get; }
    public string ChangeLabel => Change >= 0 ? $"+{Change:P0}" : Change.ToString("P0");
    public string ChangeColor => Change >= 0 ? "#16A34A" : "#DC2626";
}
