using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class ProductionCostingViewModel : ViewModelBase
{
    public ObservableCollection<ProductionOrder> Orders { get; } = new();

    [ObservableProperty] private ProductionOrder? _selectedOrder;
    [ObservableProperty] private decimal _materialCost;
    [ObservableProperty] private decimal _laborCost;
    [ObservableProperty] private decimal _overheadCost;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public ProductionCostingViewModel()
    {
        _ = LoadAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Orders.Clear();
        foreach (var x in await db.ProductionOrders.Include(x => x.FinishedProduct).OrderByDescending(x => x.OrderDate).ToListAsync()) Orders.Add(x);
    }

    partial void OnSelectedOrderChanged(ProductionOrder? value)
    {
        if (value is null) return;
        MaterialCost = value.MaterialCost;
        LaborCost = value.LaborCost;
        OverheadCost = value.OverheadCost;
    }

    [RelayCommand]
    private async Task SaveCostingAsync()
    {
        if (SelectedOrder is null)
        {
            StatusMessage = "Select a production order.";
            return;
        }

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = await db.ProductionOrders.FirstAsync(x => x.Id == SelectedOrder.Id);
        entity.MaterialCost = MaterialCost;
        entity.LaborCost = LaborCost;
        entity.OverheadCost = OverheadCost;
        await db.SaveChangesAsync();
        StatusMessage = "Production costing updated.";
        await LoadAsync();
    }
}
