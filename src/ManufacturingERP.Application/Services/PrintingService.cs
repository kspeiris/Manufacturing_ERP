using ManufacturingERP.Application.Abstractions;
using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Shared.Results;
using Microsoft.EntityFrameworkCore;
using System.Net;
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

        EnsureOutputDirectory(request.OutputPath);
        await File.WriteAllTextAsync(request.OutputPath, builder.ToString());
        return Result.Success($"Report exported to {request.OutputPath}");
    }

    public async Task<Result> ExportHtmlTableReportAsync(PrintReportRequest request, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows, string? subtitle = null)
    {
        var html = BuildHtmlReport(
            request.ReportName,
            subtitle,
            request.FilterText,
            headers,
            rows);

        EnsureOutputDirectory(request.OutputPath);
        await File.WriteAllTextAsync(request.OutputPath, html);
        return Result.Success($"Report exported to {request.OutputPath}");
    }

    public async Task<Result> ExportLatestInvoiceHtmlAsync(string outputPath)
    {
        var invoice = await _db.SalesInvoices
            .Include(x => x.Customer)
            .Include(x => x.Vehicle)
            .Include(x => x.Items).ThenInclude(x => x.Product)
            .OrderByDescending(x => x.InvoiceDate)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync();

        if (invoice is null)
            return Result.Failure("No invoice available.");

        var header = $"""
            <div class='doc-head'>
                <div>
                    <div class='eyebrow'>Manufacturing ERP</div>
                    <h1>Sales Invoice</h1>
                    <div class='meta-line'>Invoice No: {Encode(invoice.InvoiceNo)}</div>
                    <div class='meta-line'>Invoice Date: {invoice.InvoiceDate:yyyy-MM-dd HH:mm}</div>
                </div>
                <div class='bill-to'>
                    <div class='label'>Bill To</div>
                    <strong>{Encode(invoice.Customer?.ShopName ?? "Walk-in Customer")}</strong>
                    <div>{Encode(invoice.Vehicle?.VehicleNumber ?? "Vehicle N/A")}</div>
                    <div>Sale Type: {Encode(invoice.SaleType.ToString())}</div>
                </div>
            </div>
            """;

        var footer = $"""
            <div class='totals'>
                <div><span>Subtotal</span><strong>{invoice.TotalAmount:N2}</strong></div>
                <div><span>Paid</span><strong>{invoice.PaidAmount:N2}</strong></div>
                <div class='grand'><span>Balance</span><strong>{(invoice.TotalAmount - invoice.PaidAmount):N2}</strong></div>
            </div>
            """;

        var bodyRows = invoice.Items.Select(i => new[]
        {
            Encode(i.Product?.Code ?? string.Empty),
            Encode(i.Product?.Name ?? string.Empty),
            i.Quantity.ToString("N2"),
            i.UnitPrice.ToString("N2"),
            i.LineTotal.ToString("N2")
        });

        var html = BuildDocumentHtml(
            "Sales Invoice",
            header,
            new[] { "Code", "Product", "Qty", "Unit Price", "Line Total" },
            bodyRows,
            footer);

        EnsureOutputDirectory(outputPath);
        await File.WriteAllTextAsync(outputPath, html);
        return Result.Success($"Invoice HTML exported to {outputPath}");
    }

    public async Task<Result> ExportLatestPurchaseOrderHtmlAsync(string outputPath)
    {
        var po = await _db.PurchaseOrders
            .Include(x => x.Supplier)
            .Include(x => x.Items).ThenInclude(x => x.Product)
            .OrderByDescending(x => x.OrderDate)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync();

        if (po is null)
            return Result.Failure("No purchase order available.");

        var header = $"""
            <div class='doc-head'>
                <div>
                    <div class='eyebrow'>Manufacturing ERP</div>
                    <h1>Purchase Order</h1>
                    <div class='meta-line'>PO No: {Encode(po.OrderNo)}</div>
                    <div class='meta-line'>Order Date: {po.OrderDate:yyyy-MM-dd HH:mm}</div>
                </div>
                <div class='bill-to'>
                    <div class='label'>Supplier</div>
                    <strong>{Encode(po.Supplier?.Name ?? "Unknown Supplier")}</strong>
                    <div>Status: {Encode(po.Status)}</div>
                </div>
            </div>
            """;

        var footer = $"""
            <div class='totals'>
                <div class='grand'><span>Order Total</span><strong>{po.TotalAmount:N2}</strong></div>
            </div>
            """;

        var bodyRows = po.Items.Select(i => new[]
        {
            Encode(i.Product?.Code ?? string.Empty),
            Encode(i.Product?.Name ?? string.Empty),
            i.Quantity.ToString("N2"),
            i.UnitPrice.ToString("N2"),
            i.LineTotal.ToString("N2")
        });

        var html = BuildDocumentHtml(
            "Purchase Order",
            header,
            new[] { "Code", "Product", "Qty", "Unit Price", "Line Total" },
            bodyRows,
            footer);

        EnsureOutputDirectory(outputPath);
        await File.WriteAllTextAsync(outputPath, html);
        return Result.Success($"Purchase order HTML exported to {outputPath}");
    }

    public async Task<Result> ExportCustomerStatementHtmlAsync(string outputPath, string customerName, IEnumerable<CustomerLedgerRowDto> rows, DateTime? fromDate, DateTime? toDate)
    {
        var html = BuildHtmlReport(
            "Customer Statement",
            customerName,
            BuildPeriodText(fromDate, toDate),
            new[] { "Date", "Type", "Reference", "Debit", "Credit", "Balance" },
            rows.Select(r => new[]
            {
                r.EntryDate.ToString("yyyy-MM-dd"),
                Encode(r.EntryType),
                Encode(r.ReferenceNo),
                r.Debit.ToString("N2"),
                r.Credit.ToString("N2"),
                r.RunningBalance.ToString("N2")
            }));

        EnsureOutputDirectory(outputPath);
        await File.WriteAllTextAsync(outputPath, html);
        return Result.Success($"Customer statement exported to {outputPath}");
    }

    public async Task<Result> ExportSupplierStatementHtmlAsync(string outputPath, string supplierName, IEnumerable<SupplierLedgerRowDto> rows, DateTime? fromDate, DateTime? toDate)
    {
        var html = BuildHtmlReport(
            "Supplier Statement",
            supplierName,
            BuildPeriodText(fromDate, toDate),
            new[] { "Date", "Type", "Reference", "Debit", "Credit", "Balance" },
            rows.Select(r => new[]
            {
                r.EntryDate.ToString("yyyy-MM-dd"),
                Encode(r.EntryType),
                Encode(r.ReferenceNo),
                r.Debit.ToString("N2"),
                r.Credit.ToString("N2"),
                r.RunningBalance.ToString("N2")
            }));

        EnsureOutputDirectory(outputPath);
        await File.WriteAllTextAsync(outputPath, html);
        return Result.Success($"Supplier statement exported to {outputPath}");
    }

    private static string BuildHtmlReport(string title, string? subtitle, string? filterText, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        var headerHtml = $"""
            <div class='doc-head'>
                <div>
                    <div class='eyebrow'>Manufacturing ERP</div>
                    <h1>{Encode(title)}</h1>
                    {(string.IsNullOrWhiteSpace(subtitle) ? string.Empty : $"<div class='meta-line'>{Encode(subtitle)}</div>")}
                    {(string.IsNullOrWhiteSpace(filterText) ? string.Empty : $"<div class='meta-line'>Filter: {Encode(filterText)}</div>")}
                    <div class='meta-line'>Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</div>
                </div>
            </div>
            """;

        return BuildDocumentHtml(title, headerHtml, headers, rows, string.Empty);
    }

    private static string BuildDocumentHtml(string title, string headerHtml, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows, string footerHtml)
    {
        var bodyRows = string.Join(
            Environment.NewLine,
            rows.Select(row => $"<tr>{string.Join(string.Empty, row.Select(cell => $"<td>{cell}</td>"))}</tr>"));

        var headerRow = string.Join(string.Empty, headers.Select(x => $"<th>{Encode(x)}</th>"));

        return $@"
            <html>
            <head>
                <title>{Encode(title)}</title>
                <meta charset='utf-8' />
                <style>
                    body {{ font-family: Segoe UI, Arial, sans-serif; background: #f4f6fb; margin: 0; padding: 24px; color: #1f2937; }}
                    .sheet {{ max-width: 980px; margin: 0 auto; background: white; border-radius: 18px; padding: 32px; box-shadow: 0 16px 40px rgba(15, 23, 42, 0.08); }}
                    .eyebrow {{ text-transform: uppercase; letter-spacing: 0.18em; font-size: 11px; color: #2563eb; font-weight: 700; }}
                    .doc-head {{ display: flex; justify-content: space-between; gap: 24px; margin-bottom: 24px; }}
                    .doc-head h1 {{ margin: 8px 0 12px; font-size: 30px; }}
                    .bill-to {{ min-width: 240px; background: #eff6ff; border: 1px solid #bfdbfe; border-radius: 14px; padding: 16px; }}
                    .label {{ font-size: 12px; text-transform: uppercase; letter-spacing: 0.08em; color: #1d4ed8; margin-bottom: 6px; }}
                    .meta-line {{ margin: 4px 0; color: #475569; }}
                    table {{ width: 100%; border-collapse: collapse; margin-top: 12px; }}
                    thead th {{ background: #0f172a; color: white; font-size: 12px; text-transform: uppercase; letter-spacing: 0.06em; }}
                    th, td {{ border-bottom: 1px solid #e5e7eb; text-align: left; padding: 12px 10px; }}
                    tbody tr:nth-child(even) {{ background: #f8fafc; }}
                    .totals {{ display: flex; flex-direction: column; gap: 10px; margin-top: 24px; margin-left: auto; max-width: 320px; }}
                    .totals div {{ display: flex; justify-content: space-between; padding: 10px 14px; background: #f8fafc; border-radius: 12px; }}
                    .totals .grand {{ background: #dbeafe; color: #1d4ed8; font-size: 18px; font-weight: 700; }}
                </style>
            </head>
            <body>
                <div class='sheet'>
                    {headerHtml}
                    <table>
                        <thead><tr>{headerRow}</tr></thead>
                        <tbody>
                            {bodyRows}
                        </tbody>
                    </table>
                    {footerHtml}
                </div>
            </body>
            </html>";
    }

    private static void EnsureOutputDirectory(string outputPath)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
    }

    private static string BuildPeriodText(DateTime? fromDate, DateTime? toDate)
    {
        var from = fromDate.HasValue ? fromDate.Value.ToString("yyyy-MM-dd") : "Beginning";
        var to = toDate.HasValue ? toDate.Value.ToString("yyyy-MM-dd") : "Today";
        return $"Period: {from} to {to}";
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
