using CommunityToolkit.Mvvm.ComponentModel;
using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Application.Services;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly DashboardService _dashboardService;

    [ObservableProperty] private DashboardSummaryDto _summary = new();

    public DashboardViewModel(DashboardService dashboardService)
    {
        _dashboardService = dashboardService;
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        Summary = await _dashboardService.GetSummaryAsync();
    }
}
