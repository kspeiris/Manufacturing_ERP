using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Application.Services;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Domain.Enums;
using ManufacturingERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class VehicleSalesViewModel : ViewModelBase
{
    private readonly SalesService _salesService;

    public ObservableCollection<Customer> Customers { get; } = new();
    public ObservableCollection<Vehicle> Vehicles { get; } = new();
    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<InvoiceLineEditor> Lines { get; } = new();

    [ObservableProperty] private Customer? _selectedCustomer;
    [ObservableProperty] private Vehicle? _selectedVehicle;
    [ObservableProperty] private SaleType _saleType = SaleType.Cash;
    [ObservableProperty] private decimal _paidAmount;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public Array SaleTypes => Enum.GetValues(typeof(SaleType));
    public decimal InvoiceTotal => Lines.Sum(x => x.LineTotal);
    public decimal BalanceDue => Math.Max(0, InvoiceTotal - PaidAmount);

    public VehicleSalesViewModel(SalesService salesService)
    {
        _salesService = salesService;
        Lines.CollectionChanged += LinesOnCollectionChanged;
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            Customers.Clear();
            foreach (var item in await db.Customers.Where(x => x.IsActive).OrderBy(x => x.ShopName).ToListAsync()) Customers.Add(item);

            Vehicles.Clear();
            foreach (var item in await db.Vehicles.Where(x => x.IsActive).OrderBy(x => x.VehicleNumber).ToListAsync()) Vehicles.Add(item);

            Products.Clear();
            foreach (var item in await db.Products.Where(x => x.IsActive && x.SellingPrice > 0).OrderBy(x => x.Name).ToListAsync()) Products.Add(item);

            if (!Lines.Any())
                AddEmptyLine();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load vehicle sales data: {ex.Message}";
        }
    }

    [RelayCommand]
    private void AddLine() => AddEmptyLine();

    [RelayCommand]
    private async Task SaveInvoiceAsync()
    {
        if (SelectedCustomer is null || SelectedVehicle is null)
        {
            StatusMessage = "Select both customer and vehicle.";
            return;
        }

        var validLines = Lines.Where(x => x.Product is not null && x.Quantity > 0).ToList();
        if (!validLines.Any())
        {
            StatusMessage = "Add at least one invoice item.";
            return;
        }

        if (SaleType == SaleType.Cash)
            PaidAmount = InvoiceTotal;

        var request = new CreateInvoiceRequest
        {
            CustomerId = SelectedCustomer.Id,
            VehicleId = SelectedVehicle.Id,
            SaleType = SaleType,
            PaidAmount = PaidAmount,
            Items = validLines.Select(x => new CreateInvoiceLineRequest
            {
                ProductId = x.Product!.Id,
                Quantity = x.Quantity,
                UnitPrice = x.UnitPrice == 0 ? x.Product.SellingPrice : x.UnitPrice
            }).ToList()
        };

        var result = await _salesService.CreateInvoiceAsync(request);
        StatusMessage = result.IsSuccess ? $"Saved: {result.Message}" : result.Message;

        if (result.IsSuccess)
        {
            Lines.Clear();
            AddEmptyLine();
            PaidAmount = 0;
            await LoadAsync();
            NotifyTotalsChanged();
        }
    }

    partial void OnSaleTypeChanged(SaleType value)
    {
        if (value == SaleType.Cash)
            PaidAmount = InvoiceTotal;
    }

    partial void OnPaidAmountChanged(decimal value) => OnPropertyChanged(nameof(BalanceDue));

    private void AddEmptyLine()
    {
        var line = new InvoiceLineEditor();
        line.PropertyChanged += LineOnPropertyChanged;
        Lines.Add(line);
        NotifyTotalsChanged();
    }

    private void LinesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (InvoiceLineEditor line in e.OldItems)
                line.PropertyChanged -= LineOnPropertyChanged;
        }

        if (e.NewItems is not null)
        {
            foreach (InvoiceLineEditor line in e.NewItems)
                line.PropertyChanged += LineOnPropertyChanged;
        }

        NotifyTotalsChanged();
    }

    private void LineOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(InvoiceLineEditor.LineTotal) or nameof(InvoiceLineEditor.Product) or nameof(InvoiceLineEditor.Quantity) or nameof(InvoiceLineEditor.UnitPrice))
        {
            NotifyTotalsChanged();
            if (SaleType == SaleType.Cash)
                PaidAmount = InvoiceTotal;
        }
    }

    private void NotifyTotalsChanged()
    {
        OnPropertyChanged(nameof(InvoiceTotal));
        OnPropertyChanged(nameof(BalanceDue));
    }
}

public partial class InvoiceLineEditor : ObservableObject
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
