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

public partial class ProcurementViewModel : ViewModelBase
{
    private readonly ProcurementService _procurementService;
    public ObservableCollection<Supplier> Suppliers { get; } = new();
    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<Warehouse> Warehouses { get; } = new();
    public ObservableCollection<PurchaseLineEditor> Lines { get; } = new();

    [ObservableProperty] private Supplier? _selectedSupplier;
    [ObservableProperty] private Warehouse? _selectedWarehouse;
    [ObservableProperty] private string _referencePurchaseOrderNo = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public ProcurementViewModel(ProcurementService procurementService)
    {
        _procurementService = procurementService;
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            Suppliers.Clear();
            foreach (var item in await db.Suppliers.OrderBy(x => x.Name).ToListAsync()) Suppliers.Add(item);
            Products.Clear();
            foreach (var item in await db.Products.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync()) Products.Add(item);
            Warehouses.Clear();
            foreach (var item in await db.Warehouses.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync()) Warehouses.Add(item);

            if (!Lines.Any())
                Lines.Add(new PurchaseLineEditor());
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load procurement data: {ex.Message}";
        }
    }

    [RelayCommand] private void AddLine() => Lines.Add(new PurchaseLineEditor());

    [RelayCommand]
    private async Task SavePurchaseOrderAsync()
    {
        if (SelectedSupplier is null)
        {
            StatusMessage = "Select supplier.";
            return;
        }

        var validLines = Lines.Where(x => x.Product is not null && x.Quantity > 0).ToList();
        if (!validLines.Any())
        {
            StatusMessage = "Add at least one item.";
            return;
        }

        var result = await _procurementService.CreatePurchaseOrderAsync(new CreatePurchaseOrderRequest
        {
            SupplierId = SelectedSupplier.Id,
            Notes = Notes,
            Items = validLines.Select(x => new CreatePurchaseOrderLineRequest
            {
                ProductId = x.Product!.Id,
                Quantity = x.Quantity,
                UnitPrice = x.UnitPrice == 0 ? x.Product.CostPrice : x.UnitPrice
            }).ToList()
        });

        StatusMessage = result.IsSuccess ? $"Purchase order saved: {result.Message}" : result.Message;
        if (result.IsSuccess)
            ReferencePurchaseOrderNo = result.Message;
    }

    [RelayCommand]
    private async Task ReceiveGoodsAsync()
    {
        if (SelectedSupplier is null || SelectedWarehouse is null)
        {
            StatusMessage = "Select supplier and warehouse.";
            return;
        }

        var validLines = Lines.Where(x => x.Product is not null && x.Quantity > 0).ToList();
        if (!validLines.Any())
        {
            StatusMessage = "Add at least one receipt item.";
            return;
        }

        var result = await _procurementService.CreateGoodsReceiptAsync(new CreateGoodsReceiptRequest
        {
            SupplierId = SelectedSupplier.Id,
            WarehouseId = SelectedWarehouse.Id,
            PurchaseOrderNo = string.IsNullOrWhiteSpace(ReferencePurchaseOrderNo) ? null : ReferencePurchaseOrderNo.Trim(),
            Notes = Notes,
            Items = validLines.Select(x => new CreateGoodsReceiptLineRequest
            {
                ProductId = x.Product!.Id,
                Quantity = x.Quantity,
                UnitCost = x.UnitPrice == 0 ? x.Product.CostPrice : x.UnitPrice
            }).ToList()
        });

        StatusMessage = result.IsSuccess ? $"Goods receipt saved: {result.Message}" : result.Message;
        if (result.IsSuccess)
        {
            Notes = string.Empty;
            Lines.Clear();
            Lines.Add(new PurchaseLineEditor());
        }
    }
}

public partial class PurchaseLineEditor : ObservableObject
{
    [ObservableProperty] private Product? _product;
    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private decimal _unitPrice;
}
