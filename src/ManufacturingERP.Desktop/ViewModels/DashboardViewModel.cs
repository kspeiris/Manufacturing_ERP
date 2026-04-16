using CommunityToolkit.Mvvm.ComponentModel;
using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Application.Services;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly DashboardService _dashboardService;

    public ObservableCollection<DashboardChartPoint> ChartPoints { get; } = new();

    [ObservableProperty] private DashboardSummaryDto _summary = new();

    public DashboardViewModel(DashboardService dashboardService)
    {
        _dashboardService = dashboardService;
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        Summary = await _dashboardService.GetSummaryAsync();
        BuildCharts();
    }

    private void BuildCharts()
    {
        var metrics = new[]
        {
            new DashboardChartPoint("Monthly Sales", Summary.Analytics.MonthlySales, "#2563EB"),
            new DashboardChartPoint("Collections", Summary.Analytics.CollectionsThisMonth, "#0F766E"),
            new DashboardChartPoint("Inventory Value", Summary.Analytics.InventoryValue, "#7C3AED"),
            new DashboardChartPoint("Receivables", Summary.Analytics.CustomerReceivables, "#EA580C"),
            new DashboardChartPoint("Payables", Summary.Analytics.SupplierPayables, "#DC2626"),
            new DashboardChartPoint("Production Cost", Summary.Analytics.ProductionCostThisMonth, "#1D4ED8")
        };

        var maxValue = metrics.Max(x => x.Value);
        ChartPoints.Clear();

        foreach (var metric in metrics)
        {
            metric.BarWidth = maxValue <= 0 ? 18d : 40d + (double)(metric.Value / maxValue * 260m);
            ChartPoints.Add(metric);
        }
    }
}

public partial class DashboardChartPoint : ObservableObject
{
    public DashboardChartPoint(string label, decimal value, string hexColor)
    {
        Label = label;
        Value = value;
        FillBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor));
    }

    public string Label { get; }
    public decimal Value { get; }
    public SolidColorBrush FillBrush { get; }

    [ObservableProperty] private double _barWidth;
}
