using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Application.Services;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class PosSalesViewModel : ViewModelBase
{
    private readonly PosSalesService _posSalesService;
    public ObservableCollection<Customer> Customers { get; } = new();
    public ObservableCollection<Vehicle> Vehicles { get; } = new();
    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<PosLineEditor> Lines { get; } = new();

    [ObservableProperty] private Customer? _selectedCustomer;
    [ObservableProperty] private Vehicle? _selectedVehicle;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public decimal GrandTotal => Lines.Sum(x => x.LineTotal);
    public int TotalLines => Lines.Count(x => x.Product is not null && x.Quantity > 0);

    public PosSalesViewModel(PosSalesService posSalesService)
    {
        _posSalesService = posSalesService;
        Lines.CollectionChanged += LinesOnCollectionChanged;
        _ = LoadAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Customers.Clear();
            foreach (var x in await db.Customers.Where(x => x.IsActive).OrderBy(x => x.ShopName).ToListAsync()) Customers.Add(x);
            Vehicles.Clear();
            foreach (var x in await db.Vehicles.Where(x => x.IsActive).OrderBy(x => x.VehicleNumber).ToListAsync()) Vehicles.Add(x);
            Products.Clear();
            foreach (var x in await db.Products.Where(x => x.IsActive && x.SellingPrice > 0).OrderBy(x => x.Name).ToListAsync()) Products.Add(x);
            if (!Lines.Any()) AddEmptyLine();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load POS data: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand] private void AddLine() => AddEmptyLine();

    [RelayCommand]
    private async Task CheckoutAsync()
    {
        IsBusy = true;
        try
        {
        if (SelectedCustomer is null || SelectedVehicle is null)
        {
            StatusMessage = "Select customer and vehicle.";
            return;
        }

        var items = Lines.Where(x => x.Product is not null && x.Quantity > 0)
            .Select(x => new CreateInvoiceLineRequest { ProductId = x.Product!.Id, Quantity = x.Quantity, UnitPrice = x.UnitPrice <= 0 ? x.Product.SellingPrice : x.UnitPrice })
            .ToList();

        if (!items.Any())
        {
            StatusMessage = "Add items.";
            return;
        }

        var result = await _posSalesService.QuickCashSaleAsync(SelectedCustomer.Id, SelectedVehicle.Id, items, GrandTotal);
        StatusMessage = result.Message;
        if (result.IsSuccess)
        {
            Lines.Clear();
            AddEmptyLine();
            await LoadAsync();
            NotifyTotalsChanged();
        }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void AddEmptyLine()
    {
        var line = new PosLineEditor();
        line.PropertyChanged += LineOnPropertyChanged;
        Lines.Add(line);
        NotifyTotalsChanged();
    }

    private void LinesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (PosLineEditor line in e.OldItems)
                line.PropertyChanged -= LineOnPropertyChanged;
        }

        if (e.NewItems is not null)
        {
            foreach (PosLineEditor line in e.NewItems)
                line.PropertyChanged += LineOnPropertyChanged;
        }

        NotifyTotalsChanged();
    }

    private void LineOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PosLineEditor.LineTotal) or nameof(PosLineEditor.Product) or nameof(PosLineEditor.Quantity) or nameof(PosLineEditor.UnitPrice))
            NotifyTotalsChanged();
    }

    private void NotifyTotalsChanged()
    {
        OnPropertyChanged(nameof(GrandTotal));
        OnPropertyChanged(nameof(TotalLines));
    }
}

public partial class PosLineEditor : ObservableObject
{
    [ObservableProperty] private Product? _product;
    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private decimal _unitPrice;
    public decimal LineTotal => Quantity * EffectiveUnitPrice;
    public decimal EffectiveUnitPrice => UnitPrice == 0 && Product is not null ? Product.SellingPrice : UnitPrice;

    partial void OnProductChanged(Product? value)
    {
        if (value is not null && UnitPrice <= 0)
            UnitPrice = value.SellingPrice;

        OnPropertyChanged(nameof(EffectiveUnitPrice));
        OnPropertyChanged(nameof(LineTotal));
    }

    partial void OnQuantityChanged(decimal value) => OnPropertyChanged(nameof(LineTotal));
    partial void OnUnitPriceChanged(decimal value)
    {
        OnPropertyChanged(nameof(EffectiveUnitPrice));
        OnPropertyChanged(nameof(LineTotal));
    }
}
