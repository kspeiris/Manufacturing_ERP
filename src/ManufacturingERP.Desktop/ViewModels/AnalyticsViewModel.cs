using CommunityToolkit.Mvvm.ComponentModel;
using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Application.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Diagnostics;
using System.Windows.Media;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class AnalyticsViewModel : ViewModelBase
{
    private readonly AnalyticsService _analyticsService;

    [ObservableProperty] private AdvancedAnalyticsDto _analytics = new();
    [ObservableProperty] private ISeries[] _salesTrendSeries = Array.Empty<ISeries>();
    [ObservableProperty] private ISeries[] _profitTrendSeries = Array.Empty<ISeries>();
    [ObservableProperty] private ISeries[] _inventorySeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] _salesTrendXAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] _profitTrendXAxes = Array.Empty<Axis>();
    [ObservableProperty] private string[] _salesTrendLabels = Array.Empty<string>();
    [ObservableProperty] private string[] _profitTrendLabels = Array.Empty<string>();
    [ObservableProperty] private List<AnalyticsKpiCard> _kpiCards = new();
    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private string _errorMessage = string.Empty;

    public Func<double, string> CurrencyLabeler { get; } = value => $"LKR {value:N0}";
    public Func<double, string> PercentLabeler { get; } = value => $"{value:P0}";

    public AnalyticsViewModel(AnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            Analytics = await _analyticsService.GetAdvancedAnalyticsAsync();
            BuildKpiCards();
            BuildCharts();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load analytics: {ex.Message}";
            Debug.WriteLine($"[AnalyticsViewModel] Error: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
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
        var salesLabels = Analytics.MonthlySalesData.Select(x => x.Month).ToArray();
        SalesTrendLabels = salesLabels;
        SalesTrendXAxes = new[]
        {
            new Axis
            {
                Labels = salesLabels,
                LabelsRotation = 0,
                TextSize = 11
            }
        };

        SalesTrendSeries = new ISeries[]
        {
            new LineSeries<decimal>
            {
                Name = "Sales",
                Values = Analytics.MonthlySalesData.Select(x => x.Amount).ToArray(),
                Fill = null,
                GeometrySize = 8,
                Stroke = new SolidColorPaint(SKColor.Parse("#2563EB"), 3),
                GeometryStroke = new SolidColorPaint(SKColors.White, 2)
            }
        };

        var profitLabels = Analytics.MonthlyProfitData.Select(x => x.Month).ToArray();
        ProfitTrendLabels = profitLabels;
        ProfitTrendXAxes = new[]
        {
            new Axis
            {
                Labels = profitLabels,
                LabelsRotation = 0,
                TextSize = 11
            }
        };

        ProfitTrendSeries = new ISeries[]
        {
            new LineSeries<decimal>
            {
                Name = "Profit",
                Values = Analytics.MonthlyProfitData.Select(x => x.Profit).ToArray(),
                Fill = null,
                GeometrySize = 8,
                Stroke = new SolidColorPaint(SKColor.Parse("#16A34A"), 3),
                GeometryStroke = new SolidColorPaint(SKColors.White, 2)
            },
            new LineSeries<decimal>
            {
                Name = "Cost",
                Values = Analytics.MonthlyProfitData.Select(x => x.Cost).ToArray(),
                Fill = null,
                GeometrySize = 8,
                Stroke = new SolidColorPaint(SKColor.Parse("#F97316"), 3),
                GeometryStroke = new SolidColorPaint(SKColors.White, 2)
            }
        };

        InventorySeries = Analytics.InventoryByCategory
            .OrderByDescending(x => x.TotalValue)
            .Take(6)
            .Select(x => new PieSeries<decimal>
            {
                Name = x.CategoryName,
                Values = new[] { x.TotalValue },
                Fill = new SolidColorPaint(SKColor.Parse("#" + Guid.NewGuid().ToString("N").Substring(0, 6))),
                Stroke = new SolidColorPaint(SKColors.White, 1)
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
    public Brush ChangeColor => Change >= 0 ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
}
