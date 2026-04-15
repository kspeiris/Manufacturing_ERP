using ManufacturingERP.Application.Abstractions;
using ManufacturingERP.Application.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ManufacturingERP.Application.Services;

public class LedgerService
{
    private readonly IAppDbContext _db;

    public LedgerService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<List<CustomerLedgerRowDto>> GetCustomerLedgerAsync(int customerId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var rows = new List<CustomerLedgerRowDto>();

        var invoiceQuery = _db.SalesInvoices.Where(x => x.CustomerId == customerId);
        var collectionQuery = _db.CollectionEntries.Where(x => x.CustomerId == customerId);

        if (fromDate.HasValue)
        {
            var fd = fromDate.Value.Date;
            invoiceQuery = invoiceQuery.Where(x => x.InvoiceDate.Date >= fd);
            collectionQuery = collectionQuery.Where(x => x.CollectionDate.Date >= fd);
        }

        if (toDate.HasValue)
        {
            var td = toDate.Value.Date;
            invoiceQuery = invoiceQuery.Where(x => x.InvoiceDate.Date <= td);
            collectionQuery = collectionQuery.Where(x => x.CollectionDate.Date <= td);
        }

        var invoices = await invoiceQuery.OrderBy(x => x.InvoiceDate).ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.InvoiceDate,
                Ref = x.InvoiceNo,
                Debit = x.TotalAmount,
                Credit = x.PaidAmount,
                Type = x.SaleType == Domain.Enums.SaleType.Cash ? "Cash Invoice" : "Credit Invoice"
            })
            .ToListAsync();

        var collections = await collectionQuery.OrderBy(x => x.CollectionDate).ThenBy(x => x.Id)
            .Select(x => new { x.Id, Date = x.CollectionDate, Ref = x.ReferenceNo, Amount = x.Amount })
            .ToListAsync();

        decimal balance = 0;
        foreach (var item in invoices.Select(x => new { Date = x.InvoiceDate, Sort = 0, x.Id, x.Type, x.Ref, x.Debit, x.Credit })
                                     .Concat(collections.Select(x => new { Date = x.Date, Sort = 1, x.Id, Type = "Collection", Ref = x.Ref, Debit = 0m, Credit = x.Amount }))
                                     .OrderBy(x => x.Date)
                                     .ThenBy(x => x.Sort)
                                     .ThenBy(x => x.Id))
        {
            balance += item.Debit - item.Credit;
            rows.Add(new CustomerLedgerRowDto
            {
                EntryDate = item.Date,
                EntryType = item.Type,
                ReferenceNo = item.Ref,
                Debit = item.Debit,
                Credit = item.Credit,
                RunningBalance = balance
            });
        }

        return rows;
    }

    public async Task<List<SupplierLedgerRowDto>> GetSupplierLedgerAsync(int supplierId, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var rows = new List<SupplierLedgerRowDto>();

        var invoiceQuery = _db.SupplierInvoices.Where(x => x.SupplierId == supplierId);
        var paymentQuery = _db.SupplierPayments.Where(x => x.SupplierId == supplierId);

        if (fromDate.HasValue)
        {
            var fd = fromDate.Value.Date;
            invoiceQuery = invoiceQuery.Where(x => x.InvoiceDate.Date >= fd);
            paymentQuery = paymentQuery.Where(x => x.PaymentDate.Date >= fd);
        }

        if (toDate.HasValue)
        {
            var td = toDate.Value.Date;
            invoiceQuery = invoiceQuery.Where(x => x.InvoiceDate.Date <= td);
            paymentQuery = paymentQuery.Where(x => x.PaymentDate.Date <= td);
        }

        var paymentAllocations = (await paymentQuery
            .Where(x => !string.IsNullOrWhiteSpace(x.ReferenceInvoiceNo))
            .Select(x => new { x.ReferenceInvoiceNo, x.Amount })
            .ToListAsync())
            .GroupBy(x => x.ReferenceInvoiceNo!)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Amount));

        var invoices = await invoiceQuery.OrderBy(x => x.InvoiceDate).ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                Date = x.InvoiceDate,
                Ref = x.InvoiceNo,
                Debit = x.TotalAmount,
                x.PaidAmount
            })
            .ToListAsync();

        var payments = await paymentQuery.OrderBy(x => x.PaymentDate).ThenBy(x => x.Id)
            .Select(x => new { x.Id, Date = x.PaymentDate, Ref = x.PaymentNo, Amount = x.Amount })
            .ToListAsync();

        decimal balance = 0;
        foreach (var item in invoices.Select(x =>
                                     {
                                         paymentAllocations.TryGetValue(x.Ref, out var allocatedPayments);
                                         return new
                                         {
                                             x.Date,
                                             Sort = 0,
                                             x.Id,
                                             Type = "Supplier Invoice",
                                             x.Ref,
                                             x.Debit,
                                             Credit = Math.Max(0m, x.PaidAmount - allocatedPayments)
                                         };
                                     })
                                     .Concat(payments.Select(x => new { x.Date, Sort = 1, x.Id, Type = "Supplier Payment", x.Ref, Debit = 0m, Credit = x.Amount }))
                                     .OrderBy(x => x.Date)
                                     .ThenBy(x => x.Sort)
                                     .ThenBy(x => x.Id))
        {
            balance += item.Debit - item.Credit;
            rows.Add(new SupplierLedgerRowDto
            {
                EntryDate = item.Date,
                EntryType = item.Type,
                ReferenceNo = item.Ref,
                Debit = item.Debit,
                Credit = item.Credit,
                RunningBalance = balance
            });
        }

        return rows;
    }
}
