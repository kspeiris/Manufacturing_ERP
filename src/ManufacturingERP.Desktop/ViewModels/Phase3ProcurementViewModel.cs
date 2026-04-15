using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Application.Services;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class Phase3ProcurementViewModel : ViewModelBase
{
    private readonly Phase3ProcurementService _phase3ProcurementService;

    public ObservableCollection<Supplier> Suppliers { get; } = new();
    public ObservableCollection<Warehouse> Warehouses { get; } = new();
    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<SupplierInvoice> SupplierInvoices { get; } = new();
    public ObservableCollection<PurchaseReturn> PurchaseReturns { get; } = new();
    public ObservableCollection<PurchaseReturnLineEditor> ReturnLines { get; } = new();
    public ObservableCollection<GoodsReceipt> GoodsReceipts { get; } = new();

    [ObservableProperty] private Supplier? _selectedSupplier;
    [ObservableProperty] private Warehouse? _selectedWarehouse;
    [ObservableProperty] private string _referencePoNo = string.Empty;
    [ObservableProperty] private string _referenceGrnNo = string.Empty;
    [ObservableProperty] private decimal _invoiceAmount;
    [ObservableProperty] private decimal _paidAmount;
    [ObservableProperty] private string _referenceInvoiceNo = string.Empty;
    [ObservableProperty] private string _reason = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public Phase3ProcurementViewModel(Phase3ProcurementService phase3ProcurementService)
    {
        _phase3ProcurementService = phase3ProcurementService;
        _ = LoadAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Suppliers.Clear();
        foreach (var item in await db.Suppliers.OrderBy(x => x.Name).ToListAsync()) Suppliers.Add(item);
        Warehouses.Clear();
        foreach (var item in await db.Warehouses.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync()) Warehouses.Add(item);
        Products.Clear();
        foreach (var item in await db.Products.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync()) Products.Add(item);
        GoodsReceipts.Clear();
        foreach (var item in await db.GoodsReceipts.Include(x => x.Supplier).OrderByDescending(x => x.ReceiptDate).ToListAsync()) GoodsReceipts.Add(item);

        SupplierInvoices.Clear();
        foreach (var item in await _phase3ProcurementService.GetSupplierInvoicesAsync()) SupplierInvoices.Add(item);
        PurchaseReturns.Clear();
        foreach (var item in await _phase3ProcurementService.GetPurchaseReturnsAsync()) PurchaseReturns.Add(item);

        if (!ReturnLines.Any()) ReturnLines.Add(new PurchaseReturnLineEditor());
    }

    [RelayCommand]
    private void AddReturnLine() => ReturnLines.Add(new PurchaseReturnLineEditor());

    [RelayCommand]
    private async Task SaveSupplierInvoiceAsync()
    {
        if (SelectedSupplier is null || InvoiceAmount <= 0)
        {
            StatusMessage = "Select supplier and enter invoice amount.";
            return;
        }

        var result = await _phase3ProcurementService.CreateSupplierInvoiceAsync(new CreateSupplierInvoiceRequest
        {
            SupplierId = SelectedSupplier.Id,
            ReferencePoNo = ReferencePoNo,
            ReferenceGrnNo = ReferenceGrnNo,
            TotalAmount = InvoiceAmount,
            PaidAmount = PaidAmount,
            DueDate = DateTime.Today.AddDays(14)
        });

        StatusMessage = result.Message;
        if (result.IsSuccess)
        {
            InvoiceAmount = 0;
            PaidAmount = 0;
            ReferencePoNo = string.Empty;
            ReferenceGrnNo = string.Empty;
            await LoadAsync();
        }
    }

    [RelayCommand]
    private async Task SavePurchaseReturnAsync()
    {
        if (SelectedSupplier is null || SelectedWarehouse is null)
        {
            StatusMessage = "Select supplier and warehouse.";
            return;
        }

        var validLines = ReturnLines.Where(x => x.Product is not null && x.Quantity > 0).ToList();
        if (!validLines.Any())
        {
            StatusMessage = "Add at least one return line.";
            return;
        }

        var result = await _phase3ProcurementService.CreatePurchaseReturnAsync(new CreatePurchaseReturnRequest
        {
            SupplierId = SelectedSupplier.Id,
            WarehouseId = SelectedWarehouse.Id,
            ReferenceInvoiceNo = ReferenceInvoiceNo,
            Reason = Reason,
            Items = validLines.Select(x => new CreatePurchaseReturnLineRequest
            {
                ProductId = x.Product!.Id,
                Quantity = x.Quantity,
                UnitCost = x.UnitCost == 0 ? x.Product.CostPrice : x.UnitCost
            }).ToList()
        });

        StatusMessage = result.Message;
        if (result.IsSuccess)
        {
            ReferenceInvoiceNo = string.Empty;
            Reason = string.Empty;
            ReturnLines.Clear();
            ReturnLines.Add(new PurchaseReturnLineEditor());
            await LoadAsync();
        }
    }

    partial void OnSelectedSupplierChanged(Supplier? value)
    {
        if (value is null)
            return;

        var latestReceipt = GoodsReceipts.FirstOrDefault(x => x.SupplierId == value.Id);
        if (latestReceipt is not null)
        {
            ReferenceGrnNo = latestReceipt.ReceiptNo;
            ReferencePoNo = latestReceipt.PurchaseOrderNo ?? string.Empty;
        }
    }
}

public partial class PurchaseReturnLineEditor : ObservableObject
{
    [ObservableProperty] private Product? _product;
    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private decimal _unitCost;
}
