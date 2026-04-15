using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Application.Services;
using System.Collections.ObjectModel;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class ReportsViewModel : ViewModelBase
{
    private readonly ReportingService _reportingService;
    private readonly PrintingService _printingService;
    public ObservableCollection<SalesReportRowDto> SalesRows { get; } = new();
    public ObservableCollection<ProcurementReportRowDto> PurchaseRows { get; } = new();
    public ObservableCollection<LedgerSummaryRowDto> CustomerLedgerSummaryRows { get; } = new();
    public ObservableCollection<LedgerSummaryRowDto> SupplierLedgerSummaryRows { get; } = new();

    [ObservableProperty] private string _statusMessage = string.Empty;

    public ReportsViewModel(ReportingService reportingService, PrintingService printingService)
    {
        _reportingService = reportingService;
        _printingService = printingService;
        _ = LoadAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        SalesRows.Clear();
        foreach (var row in await _reportingService.GetRecentSalesAsync()) SalesRows.Add(row);

        PurchaseRows.Clear();
        foreach (var row in await _reportingService.GetRecentPurchaseOrdersAsync()) PurchaseRows.Add(row);

        CustomerLedgerSummaryRows.Clear();
        foreach (var row in await _reportingService.GetCustomerLedgerSummaryAsync()) CustomerLedgerSummaryRows.Add(row);

        SupplierLedgerSummaryRows.Clear();
        foreach (var row in await _reportingService.GetSupplierLedgerSummaryAsync()) SupplierLedgerSummaryRows.Add(row);

        StatusMessage = $"Loaded {SalesRows.Count} sales, {PurchaseRows.Count} POs, {CustomerLedgerSummaryRows.Count} customer ledgers, {SupplierLedgerSummaryRows.Count} supplier ledgers.";
    }

    [RelayCommand]
    public async Task ExportSalesAsync()
    {
        var output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports", "sales_register.txt");
        var result = await _printingService.ExportPlainTextReportAsync(
            new PrintReportRequest { ReportName = "Sales Register", OutputPath = output },
            SalesRows.Select(x => $"{x.InvoiceDate:yyyy-MM-dd} | {x.InvoiceNo} | {x.CustomerName} | {x.VehicleNo} | {x.TotalAmount:N2} | Paid {x.PaidAmount:N2} | {x.SaleType}")
        );
        StatusMessage = result.Message;
    }

    [RelayCommand]
    public async Task ExportPurchasesAsync()
    {
        var output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports", "purchase_orders.txt");
        var result = await _printingService.ExportPlainTextReportAsync(
            new PrintReportRequest { ReportName = "Purchase Order Register", OutputPath = output },
            PurchaseRows.Select(x => $"{x.OrderDate:yyyy-MM-dd} | {x.OrderNo} | {x.SupplierName} | {x.Status} | {x.TotalAmount:N2}")
        );
        StatusMessage = result.Message;
    }

    [RelayCommand]
    public async Task ExportCustomerLedgerSummaryAsync()
    {
        var output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports", "customer_ledger_summary.txt");
        var result = await _printingService.ExportPlainTextReportAsync(
            new PrintReportRequest { ReportName = "Customer Ledger Summary", OutputPath = output },
            CustomerLedgerSummaryRows.Select(x => $"{x.PartyCode} | {x.PartyName} | Balance {x.Balance:N2}")
        );
        StatusMessage = result.Message;
    }

    [RelayCommand]
    public async Task ExportSupplierLedgerSummaryAsync()
    {
        var output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports", "supplier_ledger_summary.txt");
        var result = await _printingService.ExportPlainTextReportAsync(
            new PrintReportRequest { ReportName = "Supplier Ledger Summary", OutputPath = output },
            SupplierLedgerSummaryRows.Select(x => $"{x.PartyCode} | {x.PartyName} | Balance {x.Balance:N2}")
        );
        StatusMessage = result.Message;
    }

    [RelayCommand]
    public async Task ExportInvoiceHtmlAsync()
    {
        var output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports", "latest_invoice.html");
        var result = await _printingService.ExportLatestInvoiceHtmlAsync(output);
        StatusMessage = result.Message;
    }

    [RelayCommand]
    public async Task ExportPurchaseOrderHtmlAsync()
    {
        var output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports", "latest_purchase_order.html");
        var result = await _printingService.ExportLatestPurchaseOrderHtmlAsync(output);
        StatusMessage = result.Message;
    }
}
