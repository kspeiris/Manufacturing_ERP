using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Application.Services;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class LedgersViewModel : ViewModelBase
{
    private readonly LedgerService _ledgerService;
    private readonly PrintingService _printingService;
    private readonly AuthorizationService _authorizationService;

    public ObservableCollection<Customer> Customers { get; } = new();
    public ObservableCollection<Supplier> Suppliers { get; } = new();
    public ObservableCollection<CustomerLedgerRowDto> CustomerRows { get; } = new();
    public ObservableCollection<SupplierLedgerRowDto> SupplierRows { get; } = new();

    [ObservableProperty] private Customer? _selectedCustomer;
    [ObservableProperty] private Supplier? _selectedSupplier;
    [ObservableProperty] private DateTime? _fromDate = DateTime.Today.AddDays(-30);
    [ObservableProperty] private DateTime? _toDate = DateTime.Today;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public LedgersViewModel(LedgerService ledgerService, PrintingService printingService, AuthorizationService authorizationService)
    {
        _ledgerService = ledgerService;
        _printingService = printingService;
        _authorizationService = authorizationService;
        _ = LoadMastersAsync();
    }

    [RelayCommand]
    public async Task LoadMastersAsync()
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Customers.Clear();
        foreach (var item in await db.Customers.OrderBy(x => x.ShopName).ToListAsync()) Customers.Add(item);

        Suppliers.Clear();
        foreach (var item in await db.Suppliers.OrderBy(x => x.Name).ToListAsync()) Suppliers.Add(item);
    }

    [RelayCommand]
    private async Task LoadCustomerLedgerAsync()
    {
        var auth = _authorizationService.EnsureAccountingAccess();
        if (!auth.IsSuccess) { StatusMessage = auth.Message; return; }

        CustomerRows.Clear();
        if (SelectedCustomer is null) { StatusMessage = "Select a customer."; return; }
        foreach (var row in await _ledgerService.GetCustomerLedgerAsync(SelectedCustomer.Id, FromDate, ToDate)) CustomerRows.Add(row);
        StatusMessage = $"Loaded customer ledger rows: {CustomerRows.Count}";
    }

    [RelayCommand]
    private async Task LoadSupplierLedgerAsync()
    {
        var auth = _authorizationService.EnsureAccountingAccess();
        if (!auth.IsSuccess) { StatusMessage = auth.Message; return; }

        SupplierRows.Clear();
        if (SelectedSupplier is null) { StatusMessage = "Select a supplier."; return; }
        foreach (var row in await _ledgerService.GetSupplierLedgerAsync(SelectedSupplier.Id, FromDate, ToDate)) SupplierRows.Add(row);
        StatusMessage = $"Loaded supplier ledger rows: {SupplierRows.Count}";
    }

    [RelayCommand]
    private async Task PrintCustomerStatementAsync()
    {
        var auth = _authorizationService.EnsureAccountingAccess();
        if (!auth.IsSuccess) { StatusMessage = auth.Message; return; }
        if (SelectedCustomer is null) { StatusMessage = "Select a customer."; return; }

        var output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports", $"customer_statement_{SelectedCustomer.CustomerCode}.html");
        var result = await _printingService.ExportCustomerStatementHtmlAsync(output, SelectedCustomer.ShopName, CustomerRows, FromDate, ToDate);
        StatusMessage = result.Message;
    }

    [RelayCommand]
    private async Task PrintSupplierStatementAsync()
    {
        var auth = _authorizationService.EnsureAccountingAccess();
        if (!auth.IsSuccess) { StatusMessage = auth.Message; return; }
        if (SelectedSupplier is null) { StatusMessage = "Select a supplier."; return; }

        var output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports", $"supplier_statement_{SelectedSupplier.SupplierCode}.html");
        var result = await _printingService.ExportSupplierStatementHtmlAsync(output, SelectedSupplier.Name, SupplierRows, FromDate, ToDate);
        StatusMessage = result.Message;
    }
}
