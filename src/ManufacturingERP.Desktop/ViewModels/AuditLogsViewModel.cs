using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManufacturingERP.Application.Services;
using ManufacturingERP.Domain.Entities;
using System.Collections.ObjectModel;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class AuditLogsViewModel : ViewModelBase
{
    private readonly AuditService _auditService;
    public ObservableCollection<AuditLog> AuditLogs { get; } = new();

    public AuditLogsViewModel(AuditService auditService)
    {
        _auditService = auditService;
        _ = LoadAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        AuditLogs.Clear();
        foreach (var item in await _auditService.GetRecentAsync()) AuditLogs.Add(item);
    }
}
