using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManufacturingERP.Application.Services;
using ManufacturingERP.Desktop.Services;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class WarehousesViewModel : ViewModelBase
{
    private readonly AuthorizationService _authorizationService;
    private readonly List<Warehouse> _allWarehouses = [];
    public ObservableCollection<Warehouse> Warehouses { get; } = new();
    [ObservableProperty] private Warehouse? _selectedWarehouse;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _searchText = string.Empty;

    public WarehousesViewModel(AuthorizationService authorizationService) { _authorizationService = authorizationService; _ = LoadAsync(); }

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            _allWarehouses.Clear();
            _allWarehouses.AddRange(await db.Warehouses.OrderBy(x => x.Name).ToListAsync());
            ApplyFilter();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load warehouses: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var auth = _authorizationService.EnsureAdminAccess();
        if (!auth.IsSuccess) { StatusMessage = auth.Message; return; }
        var dialog = new Views.WarehouseDialogWindow(new Warehouse { Name = "New Warehouse", Location = "", IsActive = true });
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        if (dialog.ShowDialog() == true)
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var validationMessage = ValidateWarehouse(dialog.Warehouse, db);
            if (validationMessage is not null) { StatusMessage = validationMessage; return; }

            try
            {
                db.Warehouses.Add(dialog.Warehouse);
                await db.SaveChangesAsync();
                StatusMessage = "Warehouse created.";
                await LoadAsync();
            }
            catch (DbUpdateException ex)
            {
                StatusMessage = MasterDataUiHelper.TryGetFriendlySaveError(ex, "Warehouse", "name") ??
                    "Unable to save warehouse.";
            }
        }
    }

    [RelayCommand]
    private async Task EditAsync(Warehouse? warehouse = null)
    {
        if (warehouse is not null)
            SelectedWarehouse = warehouse;
        var auth = _authorizationService.EnsureAdminAccess();
        if (!auth.IsSuccess) { StatusMessage = auth.Message; return; }
        if (SelectedWarehouse is null) { StatusMessage = "Select a warehouse."; return; }
        var clone = new Warehouse { Id = SelectedWarehouse.Id, Name = SelectedWarehouse.Name, Location = SelectedWarehouse.Location, IsActive = SelectedWarehouse.IsActive };
        var dialog = new Views.WarehouseDialogWindow(clone);
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        if (dialog.ShowDialog() == true)
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var validationMessage = ValidateWarehouse(dialog.Warehouse, db, clone.Id);
            if (validationMessage is not null) { StatusMessage = validationMessage; return; }

            try
            {
                var entity = await db.Warehouses.FirstAsync(x => x.Id == clone.Id);
                entity.Name = dialog.Warehouse.Name.Trim();
                entity.Location = dialog.Warehouse.Location.Trim();
                entity.IsActive = dialog.Warehouse.IsActive;
                await db.SaveChangesAsync();
                StatusMessage = "Warehouse updated.";
                await LoadAsync();
            }
            catch (DbUpdateException ex)
            {
                StatusMessage = MasterDataUiHelper.TryGetFriendlySaveError(ex, "Warehouse", "name") ??
                    "Unable to update warehouse.";
            }
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(Warehouse? warehouse = null)
    {
        if (warehouse is not null)
            SelectedWarehouse = warehouse;
        var auth = _authorizationService.EnsureAdminAccess();
        if (!auth.IsSuccess) { StatusMessage = auth.Message; return; }
        if (SelectedWarehouse is null) { StatusMessage = "Select a warehouse."; return; }
        if (!MasterDataUiHelper.ConfirmDelete("warehouse", SelectedWarehouse.Name))
            return;

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try
        {
            var entity = await db.Warehouses.FirstAsync(x => x.Id == SelectedWarehouse.Id);
            db.Warehouses.Remove(entity);
            await db.SaveChangesAsync();
            StatusMessage = "Warehouse deleted.";
            await LoadAsync();
        }
        catch (DbUpdateException ex)
        {
            StatusMessage = MasterDataUiHelper.TryGetFriendlySaveError(ex, "Warehouse", "name") ??
                "Unable to delete warehouse.";
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void SelectWarehouse(Warehouse? warehouse)
    {
        if (warehouse is not null)
            SelectedWarehouse = warehouse;
    }

    private void ApplyFilter()
    {
        var term = SearchText.Trim();
        var filtered = string.IsNullOrWhiteSpace(term)
            ? _allWarehouses
            : _allWarehouses.Where(x =>
                x.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.Location.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Warehouses.Clear();
        foreach (var item in filtered)
            Warehouses.Add(item);
    }

    private static string? ValidateWarehouse(Warehouse warehouse, AppDbContext db, int? currentId = null)
    {
        if (string.IsNullOrWhiteSpace(warehouse.Name))
            return "Warehouse name is required.";

        var name = warehouse.Name.Trim();
        var exists = db.Warehouses.Any(x => x.Name == name && x.Id != currentId);
        if (exists)
            return MasterDataUiHelper.GetDuplicateMessage("Warehouse", "name");

        warehouse.Name = name;
        warehouse.Location = warehouse.Location.Trim();
        return null;
    }
}
