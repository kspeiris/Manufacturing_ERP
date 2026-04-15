using ManufacturingERP.Application.Abstractions;
using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Shared.Results;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace ManufacturingERP.Application.Services;

public class PrintingService
{
    private readonly IAppDbContext _db;

    public PrintingService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result> ExportPlainTextReportAsync(PrintReportRequest request, IEnumerable<string> lines)
    {
        var builder = new StringBuilder();
        builder.AppendLine(request.ReportName);
        builder.AppendLine(new string('=', request.ReportName.Length));
        builder.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        if (!string.IsNullOrWhiteSpace(request.FilterText))
            builder.AppendLine($"Filter: {request.FilterText}");
        builder.AppendLine();

        foreach (var line in lines)
            builder.AppendLine(line);

        var dir = Path.GetDirectoryName(request.OutputPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(request.OutputPath, builder.ToString());
        return Result.Success($"Report exported to {request.OutputPath}");
    }

    public async Task<Result> ExportLatestInvoiceHtmlAsync(string outputPath)
    {
        var invoice = await _db.SalesInvoices
            .Include(x => x.Customer)
            .Include(x => x.Vehicle)
            .Include(x => x.Items).ThenInclude(x => x.Product)
            .OrderByDescending(x => x.InvoiceDate)
            .FirstOrDefaultAsync();

        if (invoice is null) return Result.Failure("No invoice available.");

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var html = $@"
<html><head><title>Invoice {invoice.InvoiceNo}</title>
<style>body{{font-family:Segoe UI;padding:30px}} table{{width:100%;border-collapse:collapse}} th,td{{border:1px solid #ddd;padding:8px}} h1{{margin-bottom:0}} .meta{{margin:12px 0 20px 0;color:#444}}</style>
</head><body>
<h1>Sales Invoice</h1>
<div class='meta'>Invoice No: {invoice.InvoiceNo}<br/>Date: {invoice.InvoiceDate:yyyy-MM-dd HH:mm}<br/>Customer: {invoice.Customer?.ShopName}<br/>Vehicle: {invoice.Vehicle?.VehicleNumber}</div>
<table><thead><tr><th>Product</th><th>Qty</th><th>Unit Price</th><th>Total</th></tr></thead>
<tbody>
{string.Join("", invoice.Items.Select(i => $"<tr><td>{i.Product?.Name}</td><td>{i.Quantity:N2}</td><td>{i.UnitPrice:N2}</td><td>{i.LineTotal:N2}</td></tr>"))}
</tbody></table>
<h3 style='text-align:right'>Grand Total: {invoice.TotalAmount:N2}</h3>
</body></html>";
        await File.WriteAllTextAsync(outputPath, html);
        return Result.Success($"Invoice HTML exported to {outputPath}");
    }

    public async Task<Result> ExportLatestPurchaseOrderHtmlAsync(string outputPath)
    {
        var po = await _db.PurchaseOrders
            .Include(x => x.Supplier)
            .Include(x => x.Items).ThenInclude(x => x.Product)
            .OrderByDescending(x => x.OrderDate)
            .FirstOrDefaultAsync();

        if (po is null) return Result.Failure("No purchase order available.");

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var html = $@"
<html><head><title>PO {po.OrderNo}</title>
<style>body{{font-family:Segoe UI;padding:30px}} table{{width:100%;border-collapse:collapse}} th,td{{border:1px solid #ddd;padding:8px}} h1{{margin-bottom:0}} .meta{{margin:12px 0 20px 0;color:#444}}</style>
</head><body>
<h1>Purchase Order</h1>
<div class='meta'>PO No: {po.OrderNo}<br/>Date: {po.OrderDate:yyyy-MM-dd HH:mm}<br/>Supplier: {po.Supplier?.Name}<br/>Status: {po.Status}</div>
<table><thead><tr><th>Product</th><th>Qty</th><th>Unit Price</th><th>Total</th></tr></thead>
<tbody>
{string.Join("", po.Items.Select(i => $"<tr><td>{i.Product?.Name}</td><td>{i.Quantity:N2}</td><td>{i.UnitPrice:N2}</td><td>{i.LineTotal:N2}</td></tr>"))}
</tbody></table>
<h3 style='text-align:right'>Grand Total: {po.TotalAmount:N2}</h3>
</body></html>";
        await File.WriteAllTextAsync(outputPath, html);
        return Result.Success($"Purchase order HTML exported to {outputPath}");
    }

    public async Task<Result> ExportCustomerStatementHtmlAsync(string outputPath, string customerName, IEnumerable<CustomerLedgerRowDto> rows, DateTime? fromDate, DateTime? toDate)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var html = $@"
<html><head><title>Customer Statement</title>
<style>body{{font-family:Segoe UI;padding:30px}} table{{width:100%;border-collapse:collapse}} th,td{{border:1px solid #ddd;padding:8px}} .meta{{margin:12px 0 20px 0;color:#444}}</style>
</head><body>
<h1>Customer Statement</h1>
<div class='meta'>Customer: {customerName}<br/>Period: {(fromDate.HasValue ? fromDate.Value.ToString("yyyy-MM-dd") : "Beginning")} to {(toDate.HasValue ? toDate.Value.ToString("yyyy-MM-dd") : "Today")}</div>
<table><thead><tr><th>Date</th><th>Type</th><th>Reference</th><th>Debit</th><th>Credit</th><th>Balance</th></tr></thead><tbody>
{string.Join("", rows.Select(r => $"<tr><td>{r.EntryDate:yyyy-MM-dd}</td><td>{r.EntryType}</td><td>{r.ReferenceNo}</td><td>{r.Debit:N2}</td><td>{r.Credit:N2}</td><td>{r.RunningBalance:N2}</td></tr>"))}
</tbody></table></body></html>";
        await File.WriteAllTextAsync(outputPath, html);
        return Result.Success($"Customer statement exported to {outputPath}");
    }

    public async Task<Result> ExportSupplierStatementHtmlAsync(string outputPath, string supplierName, IEnumerable<SupplierLedgerRowDto> rows, DateTime? fromDate, DateTime? toDate)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var html = $@"
<html><head><title>Supplier Statement</title>
<style>body{{font-family:Segoe UI;padding:30px}} table{{width:100%;border-collapse:collapse}} th,td{{border:1px solid #ddd;padding:8px}} .meta{{margin:12px 0 20px 0;color:#444}}</style>
</head><body>
<h1>Supplier Statement</h1>
<div class='meta'>Supplier: {supplierName}<br/>Period: {(fromDate.HasValue ? fromDate.Value.ToString("yyyy-MM-dd") : "Beginning")} to {(toDate.HasValue ? toDate.Value.ToString("yyyy-MM-dd") : "Today")}</div>
<table><thead><tr><th>Date</th><th>Type</th><th>Reference</th><th>Debit</th><th>Credit</th><th>Balance</th></tr></thead><tbody>
{string.Join("", rows.Select(r => $"<tr><td>{r.EntryDate:yyyy-MM-dd}</td><td>{r.EntryType}</td><td>{r.ReferenceNo}</td><td>{r.Debit:N2}</td><td>{r.Credit:N2}</td><td>{r.RunningBalance:N2}</td></tr>"))}
</tbody></table></body></html>";
        await File.WriteAllTextAsync(outputPath, html);
        return Result.Success($"Supplier statement exported to {outputPath}");
    }
}
