using ManufacturingERP.Application.Abstractions;
using ManufacturingERP.Application.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ManufacturingERP.Application.Services;

public class ReportingService
{
    private readonly IAppDbContext _db;

    public ReportingService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<List<SalesReportRowDto>> GetRecentSalesAsync()
    {
        return await _db.SalesInvoices
            .Include(x => x.Customer)
            .Include(x => x.Vehicle)
            .OrderByDescending(x => x.InvoiceDate)
            .Take(50)
            .Select(x => new SalesReportRowDto
            {
                InvoiceNo = x.InvoiceNo,
                InvoiceDate = x.InvoiceDate,
                CustomerName = x.Customer!.ShopName,
                VehicleNo = x.Vehicle!.VehicleNumber,
                TotalAmount = x.TotalAmount,
                PaidAmount = x.PaidAmount,
                SaleType = x.SaleType.ToString()
            })
            .ToListAsync();
    }

    public async Task<List<ProcurementReportRowDto>> GetRecentPurchaseOrdersAsync()
    {
        return await _db.PurchaseOrders
            .Include(x => x.Supplier)
            .OrderByDescending(x => x.OrderDate)
            .Take(50)
            .Select(x => new ProcurementReportRowDto
            {
                OrderNo = x.OrderNo,
                OrderDate = x.OrderDate,
                SupplierName = x.Supplier!.Name,
                Status = x.Status,
                TotalAmount = x.TotalAmount
            })
            .ToListAsync();
    }

    public async Task<List<LedgerSummaryRowDto>> GetCustomerLedgerSummaryAsync()
    {
        return await _db.Customers
            .OrderBy(x => x.ShopName)
            .Select(x => new LedgerSummaryRowDto
            {
                PartyCode = x.CustomerCode,
                PartyName = x.ShopName,
                Balance = x.OutstandingBalance,
                LedgerType = "Customer"
            })
            .ToListAsync();
    }

    public async Task<List<LedgerSummaryRowDto>> GetSupplierLedgerSummaryAsync()
    {
        return await _db.SupplierInvoices
            .Include(x => x.Supplier)
            .GroupBy(x => new { x.SupplierId, x.Supplier!.SupplierCode, x.Supplier!.Name })
            .Select(g => new LedgerSummaryRowDto
            {
                PartyCode = g.Key.SupplierCode,
                PartyName = g.Key.Name,
                Balance = g.Sum(x => x.BalanceAmount),
                LedgerType = "Supplier"
            })
            .OrderBy(x => x.PartyName)
            .ToListAsync();
    }
}
