using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManufacturingERP.Application.Services;
using ManufacturingERP.Domain.Entities;
using System.Collections.ObjectModel;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class MobileSyncViewModel : ViewModelBase
{
    private readonly MobileSyncService _mobileSyncService;
    public ObservableCollection<SyncLog> SyncLogs { get; } = new();

    [ObservableProperty] private string _deviceId = "HANDHELD-01";
    [ObservableProperty] private string _statusMessage = string.Empty;

    public MobileSyncViewModel(MobileSyncService mobileSyncService)
    {
        _mobileSyncService = mobileSyncService;
        _ = LoadAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            SyncLogs.Clear();
            foreach (var item in await _mobileSyncService.GetRecentSyncsAsync()) SyncLogs.Add(item);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load sync logs: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RunSyncAsync()
    {
        var result = await _mobileSyncService.StartVehicleSyncAsync(DeviceId);
        StatusMessage = result.Message;
        await LoadAsync();
    }
}
