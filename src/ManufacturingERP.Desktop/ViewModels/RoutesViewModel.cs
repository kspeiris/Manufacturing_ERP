using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManufacturingERP.Application.Services;
using ManufacturingERP.Desktop.Services;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class RoutesViewModel : ViewModelBase
{
    private readonly AuthorizationService _authorizationService;
    private readonly List<RoutePlan> _allRoutes = [];
    public ObservableCollection<RoutePlan> Routes { get; } = new();
    [ObservableProperty] private RoutePlan? _selectedRoute;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _searchText = string.Empty;

    public RoutesViewModel(AuthorizationService authorizationService) { _authorizationService = authorizationService; _ = LoadAsync(); }

    [RelayCommand]
    public async Task LoadAsync()
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _allRoutes.Clear();
        _allRoutes.AddRange(await db.RoutePlans.OrderBy(x => x.Name).ToListAsync());
        ApplyFilter();
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var auth = _authorizationService.EnsureAdminAccess();
        if (!auth.IsSuccess) { StatusMessage = auth.Message; return; }
        var dialog = new Views.RouteDialogWindow(new RoutePlan { Code = $"R{DateTime.Now:HHmmss}", Name = "New Route", Territory = "", IsActive = true });
        if (dialog.ShowDialog() == true)
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var validationMessage = ValidateRoute(dialog.RoutePlan, db);
            if (validationMessage is not null) { StatusMessage = validationMessage; return; }

            try
            {
                db.RoutePlans.Add(dialog.RoutePlan);
                await db.SaveChangesAsync();
                StatusMessage = "Route created.";
                await LoadAsync();
            }
            catch (DbUpdateException ex)
            {
                StatusMessage = MasterDataUiHelper.TryGetFriendlySaveError(ex, "Route", "code") ??
                    "Unable to save route.";
            }
        }
    }

    [RelayCommand]
    private async Task EditAsync()
    {
        var auth = _authorizationService.EnsureAdminAccess();
        if (!auth.IsSuccess) { StatusMessage = auth.Message; return; }
        if (SelectedRoute is null) { StatusMessage = "Select a route."; return; }
        var clone = new RoutePlan { Id = SelectedRoute.Id, Code = SelectedRoute.Code, Name = SelectedRoute.Name, Territory = SelectedRoute.Territory, Notes = SelectedRoute.Notes, IsActive = SelectedRoute.IsActive };
        var dialog = new Views.RouteDialogWindow(clone);
        if (dialog.ShowDialog() == true)
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var validationMessage = ValidateRoute(dialog.RoutePlan, db, clone.Id);
            if (validationMessage is not null) { StatusMessage = validationMessage; return; }

            try
            {
                var entity = await db.RoutePlans.FirstAsync(x => x.Id == clone.Id);
                entity.Code = dialog.RoutePlan.Code.Trim();
                entity.Name = dialog.RoutePlan.Name.Trim();
                entity.Territory = dialog.RoutePlan.Territory.Trim();
                entity.Notes = dialog.RoutePlan.Notes?.Trim();
                entity.IsActive = dialog.RoutePlan.IsActive;
                await db.SaveChangesAsync();
                StatusMessage = "Route updated.";
                await LoadAsync();
            }
            catch (DbUpdateException ex)
            {
                StatusMessage = MasterDataUiHelper.TryGetFriendlySaveError(ex, "Route", "code") ??
                    "Unable to update route.";
            }
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        var auth = _authorizationService.EnsureAdminAccess();
        if (!auth.IsSuccess) { StatusMessage = auth.Message; return; }
        if (SelectedRoute is null) { StatusMessage = "Select a route."; return; }
        if (!MasterDataUiHelper.ConfirmDelete("route", SelectedRoute.Name))
            return;

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try
        {
            var entity = await db.RoutePlans.FirstAsync(x => x.Id == SelectedRoute.Id);
            db.RoutePlans.Remove(entity);
            await db.SaveChangesAsync();
            StatusMessage = "Route deleted.";
            await LoadAsync();
        }
        catch (DbUpdateException ex)
        {
            StatusMessage = MasterDataUiHelper.TryGetFriendlySaveError(ex, "Route", "code") ??
                "Unable to delete route.";
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var term = SearchText.Trim();
        var filtered = string.IsNullOrWhiteSpace(term)
            ? _allRoutes
            : _allRoutes.Where(x =>
                x.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.Territory.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (x.Notes?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();

        Routes.Clear();
        foreach (var item in filtered)
            Routes.Add(item);
    }

    private static string? ValidateRoute(RoutePlan routePlan, AppDbContext db, int? currentId = null)
    {
        if (string.IsNullOrWhiteSpace(routePlan.Code))
            return "Route code is required.";
        if (string.IsNullOrWhiteSpace(routePlan.Name))
            return "Route name is required.";
        if (string.IsNullOrWhiteSpace(routePlan.Territory))
            return "Territory is required.";

        var code = routePlan.Code.Trim();
        var exists = db.RoutePlans.Any(x => x.Code == code && x.Id != currentId);
        if (exists)
            return MasterDataUiHelper.GetDuplicateMessage("Route", "code");

        routePlan.Code = code;
        routePlan.Name = routePlan.Name.Trim();
        routePlan.Territory = routePlan.Territory.Trim();
        routePlan.Notes = routePlan.Notes?.Trim();
        return null;
    }
}
