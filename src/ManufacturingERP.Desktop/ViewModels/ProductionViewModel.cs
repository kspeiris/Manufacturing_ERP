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

public partial class ProductionViewModel : ViewModelBase
{
    private readonly ProductionService _productionService;

    public ObservableCollection<Product> FinishedProducts { get; } = new();
    public ObservableCollection<Warehouse> Warehouses { get; } = new();
    public ObservableCollection<ProductionOrder> ProductionOrders { get; } = new();

    [ObservableProperty] private Product? _selectedFinishedProduct;
    [ObservableProperty] private Warehouse? _selectedWarehouse;
    [ObservableProperty] private ProductionOrder? _selectedOrder;
    [ObservableProperty] private decimal _plannedQuantity;
    [ObservableProperty] private decimal _receiptQuantity;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public ProductionViewModel(ProductionService productionService)
    {
        _productionService = productionService;
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        FinishedProducts.Clear();
        foreach (var item in await db.Products
                     .Include(x => x.ProductCategory)
                     .Where(x => x.ProductCategory!.Name == "Finished Goods")
                     .OrderBy(x => x.Name)
                     .ToListAsync())
            FinishedProducts.Add(item);

        Warehouses.Clear();
        foreach (var item in await db.Warehouses.OrderBy(x => x.Name).ToListAsync()) Warehouses.Add(item);

        await RefreshOrdersAsync();
    }

    [RelayCommand]
    public async Task RefreshOrdersAsync()
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        ProductionOrders.Clear();
        foreach (var item in await db.ProductionOrders.Include(x => x.FinishedProduct).OrderByDescending(x => x.OrderDate).ToListAsync())
            ProductionOrders.Add(item);
    }

    [RelayCommand]
    private async Task CreateOrderAsync()
    {
        if (SelectedFinishedProduct is null || PlannedQuantity <= 0)
        {
            StatusMessage = "Select product and enter a planned quantity.";
            return;
        }

        var result = await _productionService.CreateProductionOrderAsync(new CreateProductionOrderRequest
        {
            FinishedProductId = SelectedFinishedProduct.Id,
            PlannedQuantity = PlannedQuantity
        });

        StatusMessage = result.IsSuccess ? $"Production order created: {result.Message}" : result.Message;
        if (result.IsSuccess)
        {
            PlannedQuantity = 0;
            await RefreshOrdersAsync();
        }
    }

    [RelayCommand]
    private async Task ReceiveFinishedGoodsAsync()
    {
        if (SelectedOrder is null || SelectedWarehouse is null || ReceiptQuantity <= 0)
        {
            StatusMessage = "Select order, warehouse, and receipt quantity.";
            return;
        }

        var result = await _productionService.ReceiveFinishedGoodsAsync(new ReceiveFinishedGoodsRequest { ProductionOrderId = SelectedOrder.Id, WarehouseId = SelectedWarehouse.Id, ProducedQuantity = ReceiptQuantity, ScrapQuantity = 0, BatchNo = string.Empty });
        StatusMessage = result.Message;
        if (result.IsSuccess)
        {
            ReceiptQuantity = 0;
            await RefreshOrdersAsync();
        }
    }
}

