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
    public ObservableCollection<StockReportRowDto> StockRows { get; } = new();
    public ObservableCollection<ProductionReportRowDto> ProductionRows { get; } = new();
    public ObservableCollection<LedgerSummaryRowDto> CustomerLedgerSummaryRows { get; } = new();
    public ObservableCollection<LedgerSummaryRowDto> SupplierLedgerSummaryRows { get; } = new();

    [ObservableProperty] private DateTime? _fromDate = DateTime.Today.AddDays(-30);
    [ObservableProperty] private DateTime? _toDate = DateTime.Today;
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
        IsBusy = true;
        try
        {
        LoadRows(SalesRows, await _reportingService.GetSalesRegisterAsync(FromDate, ToDate));
        LoadRows(PurchaseRows, await _reportingService.GetPurchaseRegisterAsync(FromDate, ToDate));
        LoadRows(StockRows, await _reportingService.GetStockReportAsync());
        LoadRows(ProductionRows, await _reportingService.GetProductionReportAsync(FromDate, ToDate));
        LoadRows(CustomerLedgerSummaryRows, await _reportingService.GetCustomerLedgerSummaryAsync());
        LoadRows(SupplierLedgerSummaryRows, await _reportingService.GetSupplierLedgerSummaryAsync());

        StatusMessage =
            $"Loaded {SalesRows.Count} sales, {PurchaseRows.Count} purchases, {StockRows.Count} stock rows, {ProductionRows.Count} production rows.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ExportSalesRegisterHtmlAsync()
    {
        IsBusy = true;
        try
        {
        var output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports", "sales_register.html");
        var result = await _printingService.ExportHtmlTableReportAsync(
            new PrintReportRequest
            {
                ReportName = "Sales Register",
                OutputPath = output,
                FilterText = BuildDateFilterText()
            },
            new[] { "Date", "Invoice", "Customer", "Vehicle", "Amount", "Paid", "Type" },
            SalesRows.Select(x => (IReadOnlyList<string>)new[]
            {
                x.InvoiceDate.ToString("yyyy-MM-dd"),
                x.InvoiceNo,
                x.CustomerName,
                x.VehicleNo,
                $"LKR {x.TotalAmount:N2}",
                $"LKR {x.PaidAmount:N2}",
                x.SaleType
            }),
            "Printable sales activity register");
        StatusMessage = result.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ExportPurchaseRegisterHtmlAsync()
    {
        IsBusy = true;
        try
        {
        var output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports", "purchase_register.html");
        var result = await _printingService.ExportHtmlTableReportAsync(
            new PrintReportRequest
            {
                ReportName = "Purchase Register",
                OutputPath = output,
                FilterText = BuildDateFilterText()
            },
            new[] { "Date", "PO No", "Supplier", "Status", "Total" },
            PurchaseRows.Select(x => (IReadOnlyList<string>)new[]
            {
                x.OrderDate.ToString("yyyy-MM-dd"),
                x.OrderNo,
                x.SupplierName,
                x.Status,
                $"LKR {x.TotalAmount:N2}"
            }),
            "Procurement orders by date");
        StatusMessage = result.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ExportStockReportHtmlAsync()
    {
        IsBusy = true;
        try
        {
        var output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports", "stock_report.html");
        var result = await _printingService.ExportHtmlTableReportAsync(
            new PrintReportRequest
            {
                ReportName = "Stock Report",
                OutputPath = output,
                FilterText = "Current on-hand balances by warehouse"
            },
            new[] { "Code", "Product", "Warehouse", "Qty", "Unit Cost", "Value" },
            StockRows.Select(x => (IReadOnlyList<string>)new[]
            {
                x.ProductCode,
                x.ProductName,
                x.WarehouseName,
                x.QuantityOnHand.ToString("N2"),
                $"LKR {x.UnitCost:N2}",
                $"LKR {x.StockValue:N2}"
            }),
            "Snapshot of available stock");
        StatusMessage = result.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ExportProductionReportHtmlAsync()
    {
        IsBusy = true;
        try
        {
        var output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports", "production_report.html");
        var result = await _printingService.ExportHtmlTableReportAsync(
            new PrintReportRequest
            {
                ReportName = "Production Report",
                OutputPath = output,
                FilterText = BuildDateFilterText()
            },
            new[] { "Date", "Order", "Product", "Batch", "Produced", "Scrap", "Total Cost", "Unit Cost", "Status" },
            ProductionRows.Select(x => (IReadOnlyList<string>)new[]
            {
                x.OrderDate.ToString("yyyy-MM-dd"),
                x.OrderNo,
                x.ProductName,
                x.BatchNo,
                x.ProducedQuantity.ToString("N2"),
                x.ScrapQuantity.ToString("N2"),
                $"LKR {x.TotalCost:N2}",
                $"LKR {x.UnitCost:N2}",
                x.Status
            }),
            "Finished goods and costing summary");
        StatusMessage = result.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ExportCustomerLedgerSummaryAsync()
    {
        IsBusy = true;
        try
        {
        var output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports", "customer_ledger_summary.html");
        var result = await _printingService.ExportHtmlTableReportAsync(
            new PrintReportRequest
            {
                ReportName = "Customer Outstanding Summary",
                OutputPath = output
            },
            new[] { "Code", "Customer", "Balance" },
            CustomerLedgerSummaryRows.Select(x => (IReadOnlyList<string>)new[]
            {
                x.PartyCode,
                x.PartyName,
                $"LKR {x.Balance:N2}"
            }),
            "Open receivables by customer");
        StatusMessage = result.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ExportSupplierLedgerSummaryAsync()
    {
        IsBusy = true;
        try
        {
        var output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports", "supplier_ledger_summary.html");
        var result = await _printingService.ExportHtmlTableReportAsync(
            new PrintReportRequest
            {
                ReportName = "Supplier Outstanding Summary",
                OutputPath = output
            },
            new[] { "Code", "Supplier", "Balance" },
            SupplierLedgerSummaryRows.Select(x => (IReadOnlyList<string>)new[]
            {
                x.PartyCode,
                x.PartyName,
                $"LKR {x.Balance:N2}"
            }),
            "Open payables by supplier");
        StatusMessage = result.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ExportInvoiceHtmlAsync()
    {
        IsBusy = true;
        try
        {
        var output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports", "latest_invoice.html");
        var result = await _printingService.ExportLatestInvoiceHtmlAsync(output);
        StatusMessage = result.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ExportPurchaseOrderHtmlAsync()
    {
        IsBusy = true;
        try
        {
        var output = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports", "latest_purchase_order.html");
        var result = await _printingService.ExportLatestPurchaseOrderHtmlAsync(output);
        StatusMessage = result.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static void LoadRows<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var row in source)
            target.Add(row);
    }

    private string BuildDateFilterText()
    {
        var from = FromDate.HasValue ? FromDate.Value.ToString("yyyy-MM-dd") : "Beginning";
        var to = ToDate.HasValue ? ToDate.Value.ToString("yyyy-MM-dd") : "Today";
        return $"{from} to {to}";
    }
}
