using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Application.Services;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class SupplierPaymentsViewModel : ViewModelBase
{
    private readonly SupplierPaymentService _supplierPaymentService;
    private readonly MasterDataValidationService _validationService;
    public ObservableCollection<Supplier> Suppliers { get; } = new();
    public ObservableCollection<SupplierPayment> Payments { get; } = new();
    public ObservableCollection<SupplierInvoice> OpenInvoices { get; } = new();

    [ObservableProperty] private Supplier? _selectedSupplier;
    [ObservableProperty] private SupplierInvoice? _selectedInvoice;
    [ObservableProperty] private string _referenceInvoiceNo = string.Empty;
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private string _paymentMethod = "Cash";
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    public decimal SelectedInvoiceBalance => SelectedInvoice?.BalanceAmount ?? 0m;

    public SupplierPaymentsViewModel(SupplierPaymentService supplierPaymentService, MasterDataValidationService validationService)
    {
        _supplierPaymentService = supplierPaymentService;
        _validationService = validationService;
        _ = LoadAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Suppliers.Clear();
        foreach (var item in await db.Suppliers.OrderBy(x => x.Name).ToListAsync()) Suppliers.Add(item);

        Payments.Clear();
        foreach (var item in await _supplierPaymentService.GetRecentAsync()) Payments.Add(item);

        await LoadOpenInvoicesAsync();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedSupplier is null)
        {
            StatusMessage = "Select supplier.";
            return;
        }

        var amountCheck = _validationService.ValidateNonNegative("Amount", Amount);
        if (!amountCheck.ok || Amount <= 0)
        {
            StatusMessage = "Enter a valid amount.";
            return;
        }

        var methodCheck = _validationService.ValidateRequired("Payment Method", PaymentMethod);
        if (!methodCheck.ok)
        {
            StatusMessage = methodCheck.message;
            return;
        }

        var result = await _supplierPaymentService.SaveAsync(new CreateSupplierPaymentRequest
        {
            SupplierId = SelectedSupplier.Id,
            ReferenceInvoiceNo = string.IsNullOrWhiteSpace(ReferenceInvoiceNo) ? null : ReferenceInvoiceNo.Trim(),
            Amount = Amount,
            PaymentMethod = PaymentMethod,
            Notes = Notes
        });

        StatusMessage = result.Message;
        if (result.IsSuccess)
        {
            ReferenceInvoiceNo = string.Empty;
            Amount = 0;
            Notes = string.Empty;
            SelectedInvoice = null;
            await LoadAsync();
        }
    }

    partial void OnSelectedSupplierChanged(Supplier? value)
    {
        SelectedInvoice = null;
        ReferenceInvoiceNo = string.Empty;
        Amount = 0;
        _ = LoadOpenInvoicesAsync();
    }

    partial void OnSelectedInvoiceChanged(SupplierInvoice? value)
    {
        ReferenceInvoiceNo = value?.InvoiceNo ?? string.Empty;
        OnPropertyChanged(nameof(SelectedInvoiceBalance));
    }

    private async Task LoadOpenInvoicesAsync()
    {
        OpenInvoices.Clear();
        if (SelectedSupplier is null)
        {
            OnPropertyChanged(nameof(SelectedInvoiceBalance));
            return;
        }

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var invoices = await db.SupplierInvoices
            .Where(x => x.SupplierId == SelectedSupplier.Id && x.BalanceAmount > 0)
            .OrderBy(x => x.InvoiceDate)
            .ToListAsync();

        foreach (var item in invoices)
            OpenInvoices.Add(item);

        OnPropertyChanged(nameof(SelectedInvoiceBalance));
    }
}
