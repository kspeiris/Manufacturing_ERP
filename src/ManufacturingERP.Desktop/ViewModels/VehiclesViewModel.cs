using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManufacturingERP.Application.Services;
using ManufacturingERP.Desktop.Services;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class VehiclesViewModel : ViewModelBase
{
    private readonly AuthorizationService _authorizationService;
    private readonly List<Vehicle> _allVehicles = [];
    public ObservableCollection<Vehicle> Vehicles { get; } = new();
    [ObservableProperty] private Vehicle? _selectedVehicle;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _searchText = string.Empty;

    public VehiclesViewModel(AuthorizationService authorizationService) { _authorizationService = authorizationService; _ = LoadAsync(); }

    [RelayCommand]
    public async Task LoadAsync()
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _allVehicles.Clear();
        _allVehicles.AddRange(await db.Vehicles.OrderBy(x => x.VehicleNumber).ToListAsync());
        ApplyFilter();
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var auth = _authorizationService.EnsureAdminAccess();
        if (!auth.IsSuccess) { StatusMessage = auth.Message; return; }
        var dialog = new Views.VehicleDialogWindow(new Vehicle { VehicleNumber = "NEW-0000", Description = "Vehicle", IsActive = true });
        if (dialog.ShowDialog() == true)
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var validationMessage = ValidateVehicle(dialog.Vehicle, db);
            if (validationMessage is not null) { StatusMessage = validationMessage; return; }

            try
            {
                db.Vehicles.Add(dialog.Vehicle);
                await db.SaveChangesAsync();
                StatusMessage = "Vehicle created.";
                await LoadAsync();
            }
            catch (DbUpdateException ex)
            {
                StatusMessage = MasterDataUiHelper.TryGetFriendlySaveError(ex, "Vehicle", "number") ??
                    "Unable to save vehicle.";
            }
        }
    }

    [RelayCommand]
    private async Task EditAsync(Vehicle? vehicle = null)
    {
        if (vehicle is not null)
            SelectedVehicle = vehicle;
        var auth = _authorizationService.EnsureAdminAccess();
        if (!auth.IsSuccess) { StatusMessage = auth.Message; return; }
        if (SelectedVehicle is null) { StatusMessage = "Select a vehicle."; return; }
        var clone = new Vehicle { Id = SelectedVehicle.Id, VehicleNumber = SelectedVehicle.VehicleNumber, Description = SelectedVehicle.Description, DriverName = SelectedVehicle.DriverName, SalesRepName = SelectedVehicle.SalesRepName, IsActive = SelectedVehicle.IsActive };
        var dialog = new Views.VehicleDialogWindow(clone);
        if (dialog.ShowDialog() == true)
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var validationMessage = ValidateVehicle(dialog.Vehicle, db, clone.Id);
            if (validationMessage is not null) { StatusMessage = validationMessage; return; }

            try
            {
                var entity = await db.Vehicles.FirstAsync(x => x.Id == clone.Id);
                entity.VehicleNumber = dialog.Vehicle.VehicleNumber.Trim();
                entity.Description = dialog.Vehicle.Description.Trim();
                entity.DriverName = dialog.Vehicle.DriverName.Trim();
                entity.SalesRepName = dialog.Vehicle.SalesRepName.Trim();
                entity.IsActive = dialog.Vehicle.IsActive;
                await db.SaveChangesAsync();
                StatusMessage = "Vehicle updated.";
                await LoadAsync();
            }
            catch (DbUpdateException ex)
            {
                StatusMessage = MasterDataUiHelper.TryGetFriendlySaveError(ex, "Vehicle", "number") ??
                    "Unable to update vehicle.";
            }
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(Vehicle? vehicle = null)
    {
        if (vehicle is not null)
            SelectedVehicle = vehicle;
        var auth = _authorizationService.EnsureAdminAccess();
        if (!auth.IsSuccess) { StatusMessage = auth.Message; return; }
        if (SelectedVehicle is null) { StatusMessage = "Select a vehicle."; return; }
        if (!MasterDataUiHelper.ConfirmDelete("vehicle", SelectedVehicle.VehicleNumber))
            return;

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try
        {
            var entity = await db.Vehicles.FirstAsync(x => x.Id == SelectedVehicle.Id);
            db.Vehicles.Remove(entity);
            await db.SaveChangesAsync();
            StatusMessage = "Vehicle deleted.";
            await LoadAsync();
        }
        catch (DbUpdateException ex)
        {
            StatusMessage = MasterDataUiHelper.TryGetFriendlySaveError(ex, "Vehicle", "number") ??
                "Unable to delete vehicle.";
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void SelectVehicle(Vehicle? vehicle)
    {
        if (vehicle is not null)
            SelectedVehicle = vehicle;
    }

    private void ApplyFilter()
    {
        var term = SearchText.Trim();
        var filtered = string.IsNullOrWhiteSpace(term)
            ? _allVehicles
            : _allVehicles.Where(x =>
                x.VehicleNumber.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.Description.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.DriverName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.SalesRepName.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Vehicles.Clear();
        foreach (var item in filtered)
            Vehicles.Add(item);
    }

    private static string? ValidateVehicle(Vehicle vehicle, AppDbContext db, int? currentId = null)
    {
        if (string.IsNullOrWhiteSpace(vehicle.VehicleNumber))
            return "Vehicle number is required.";
        if (string.IsNullOrWhiteSpace(vehicle.Description))
            return "Description is required.";

        var number = vehicle.VehicleNumber.Trim();
        var exists = db.Vehicles.Any(x => x.VehicleNumber == number && x.Id != currentId);
        if (exists)
            return MasterDataUiHelper.GetDuplicateMessage("Vehicle", "number");

        vehicle.VehicleNumber = number;
        vehicle.Description = vehicle.Description.Trim();
        vehicle.DriverName = vehicle.DriverName.Trim();
        vehicle.SalesRepName = vehicle.SalesRepName.Trim();
        return null;
    }
}
