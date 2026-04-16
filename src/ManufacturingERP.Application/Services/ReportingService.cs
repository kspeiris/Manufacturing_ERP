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

    public async Task<List<SalesReportRowDto>> GetSalesRegisterAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _db.SalesInvoices
            .Include(x => x.Customer)
            .Include(x => x.Vehicle)
            .AsQueryable();

        if (fromDate.HasValue)
        {
            var from = fromDate.Value.Date;
            query = query.Where(x => x.InvoiceDate.Date >= from);
        }

        if (toDate.HasValue)
        {
            var to = toDate.Value.Date;
            query = query.Where(x => x.InvoiceDate.Date <= to);
        }

        return await query
            .OrderByDescending(x => x.InvoiceDate)
            .ThenByDescending(x => x.Id)
            .Select(x => new SalesReportRowDto
            {
                InvoiceNo = x.InvoiceNo,
                InvoiceDate = x.InvoiceDate,
                CustomerName = x.Customer != null ? x.Customer.ShopName : string.Empty,
                VehicleNo = x.Vehicle != null ? x.Vehicle.VehicleNumber : string.Empty,
                TotalAmount = x.TotalAmount,
                PaidAmount = x.PaidAmount,
                SaleType = x.SaleType.ToString()
            })
            .ToListAsync();
    }

    public async Task<List<ProcurementReportRowDto>> GetPurchaseRegisterAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _db.PurchaseOrders
            .Include(x => x.Supplier)
            .AsQueryable();

        if (fromDate.HasValue)
        {
            var from = fromDate.Value.Date;
            query = query.Where(x => x.OrderDate.Date >= from);
        }

        if (toDate.HasValue)
        {
            var to = toDate.Value.Date;
            query = query.Where(x => x.OrderDate.Date <= to);
        }

        return await query
            .OrderByDescending(x => x.OrderDate)
            .ThenByDescending(x => x.Id)
            .Select(x => new ProcurementReportRowDto
            {
                OrderNo = x.OrderNo,
                OrderDate = x.OrderDate,
                SupplierName = x.Supplier != null ? x.Supplier.Name : string.Empty,
                Status = x.Status,
                TotalAmount = x.TotalAmount
            })
            .ToListAsync();
    }

    public async Task<List<StockReportRowDto>> GetStockReportAsync()
    {
        return await _db.StockBalances
            .Include(x => x.Product)
            .Include(x => x.Warehouse)
            .OrderBy(x => x.Product!.Code)
            .ThenBy(x => x.Warehouse!.Name)
            .Select(x => new StockReportRowDto
            {
                ProductCode = x.Product != null ? x.Product.Code : string.Empty,
                ProductName = x.Product != null ? x.Product.Name : string.Empty,
                WarehouseName = x.Warehouse != null ? x.Warehouse.Name : string.Empty,
                QuantityOnHand = x.QuantityOnHand,
                UnitCost = x.Product != null ? x.Product.CostPrice : 0m,
                StockValue = x.QuantityOnHand * (x.Product != null ? x.Product.CostPrice : 0m)
            })
            .ToListAsync();
    }

    public async Task<List<ProductionReportRowDto>> GetProductionReportAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _db.ProductionOrders
            .Include(x => x.FinishedProduct)
            .AsQueryable();

        if (fromDate.HasValue)
        {
            var from = fromDate.Value.Date;
            query = query.Where(x => x.OrderDate.Date >= from);
        }

        if (toDate.HasValue)
        {
            var to = toDate.Value.Date;
            query = query.Where(x => x.OrderDate.Date <= to);
        }

        return await query
            .OrderByDescending(x => x.OrderDate)
            .ThenByDescending(x => x.Id)
            .Select(x => new ProductionReportRowDto
            {
                OrderNo = x.OrderNo,
                OrderDate = x.OrderDate,
                ProductName = x.FinishedProduct != null ? x.FinishedProduct.Name : string.Empty,
                BatchNo = x.BatchNo,
                PlannedQuantity = x.PlannedQuantity,
                ProducedQuantity = x.ProducedQuantity,
                ScrapQuantity = x.ScrapQuantity,
                TotalCost = x.MaterialCost + x.LaborCost + x.OverheadCost,
                UnitCost = x.ProducedQuantity == 0 ? 0 : (x.MaterialCost + x.LaborCost + x.OverheadCost) / x.ProducedQuantity,
                Status = x.Status
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
