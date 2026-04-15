using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Application.Services;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class WarehouseViewModel : ViewModelBase
{
    private readonly WarehouseService _warehouseService;

    public ObservableCollection<StockRowDto> StockRows { get; } = new();
    public ObservableCollection<StockMovementRowDto> MovementRows { get; } = new();
    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<Warehouse> Warehouses { get; } = new();

    [ObservableProperty] private Product? _selectedProduct;
    [ObservableProperty] private Warehouse? _selectedWarehouse;
    [ObservableProperty] private decimal _adjustmentQty;
    [ObservableProperty] private string _reason = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    public decimal TotalQuantityOnHand => StockRows.Sum(x => x.QuantityOnHand);
    public decimal TotalStockValue => StockRows.Sum(x => x.StockValue);

    public WarehouseViewModel(WarehouseService warehouseService)
    {
        _warehouseService = warehouseService;
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Products.Clear();
        foreach (var item in await db.Products.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync()) Products.Add(item);

        Warehouses.Clear();
        foreach (var item in await db.Warehouses.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync()) Warehouses.Add(item);

        await RefreshStockAsync();
    }

    [RelayCommand]
    public async Task RefreshStockAsync()
    {
        StockRows.Clear();
        foreach (var row in await _warehouseService.GetStockAsync()) StockRows.Add(row);
        MovementRows.Clear();
        foreach (var row in await _warehouseService.GetStockMovementsAsync(SelectedProduct?.Id, SelectedWarehouse?.Id)) MovementRows.Add(row);
        OnPropertyChanged(nameof(TotalQuantityOnHand));
        OnPropertyChanged(nameof(TotalStockValue));
    }

    [RelayCommand]
    private async Task SaveAdjustmentAsync()
    {
        if (SelectedProduct is null || SelectedWarehouse is null)
        {
            StatusMessage = "Select product and warehouse.";
            return;
        }

        var result = await _warehouseService.CreateAdjustmentAsync(SelectedProduct.Id, SelectedWarehouse.Id, AdjustmentQty, Reason);
        StatusMessage = result.Message;
        if (result.IsSuccess)
        {
            AdjustmentQty = 0;
            Reason = string.Empty;
            await RefreshStockAsync();
        }
    }

    partial void OnSelectedProductChanged(Product? value) => _ = RefreshStockAsync();
    partial void OnSelectedWarehouseChanged(Warehouse? value) => _ = RefreshStockAsync();
}
