using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManufacturingERP.Application.Services;
using ManufacturingERP.Domain.Entities;
using System.Collections.ObjectModel;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class AuditLogsViewModel : ViewModelBase
{
    private readonly AuditService _auditService;
    private readonly List<AuditLog> _allAuditLogs = [];

    public ObservableCollection<AuditLog> AuditLogs { get; } = new();

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public AuditLogsViewModel(AuditService auditService)
    {
        _auditService = auditService;
        _ = LoadAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            _allAuditLogs.Clear();
            _allAuditLogs.AddRange(await _auditService.GetRecentAsync());
            ApplyFilter();
            StatusMessage = $"Loaded {_allAuditLogs.Count} audit entries.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load audit logs: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var term = SearchText.Trim();
        var filtered = string.IsNullOrWhiteSpace(term)
            ? _allAuditLogs
            : _allAuditLogs.Where(x =>
                (x.User?.Username ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.Action.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.EntityName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.EntityKey.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (x.NewValues ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (x.OldValues ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToList();

        AuditLogs.Clear();
        foreach (var item in filtered)
            AuditLogs.Add(item);
    }
}
