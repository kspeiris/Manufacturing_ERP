using ManufacturingERP.Application.Abstractions;
using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Domain.Enums;
using ManufacturingERP.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace ManufacturingERP.Application.Services;

public class SalesService
{
    private readonly IAppDbContext _db;
    private readonly AuthorizationService _authorizationService;
    private readonly AuditService _auditService;
    private readonly CurrentUserService _currentUserService;

    public SalesService(IAppDbContext db, AuthorizationService authorizationService, AuditService auditService, CurrentUserService currentUserService)
    {
        _db = db;
        _authorizationService = authorizationService;
        _auditService = auditService;
        _currentUserService = currentUserService;
    }

    public async Task<Result<int>> CreateInvoiceAsync(CreateInvoiceRequest request, int? actorUserId = null)
    {
        var auth = _authorizationService.EnsureSalesPostAccess();
        if (!auth.IsSuccess)
            return Result<int>.Failure(auth.Message);

        if (!request.Items.Any())
            return Result<int>.Failure("Invoice must contain at least one item.");

        if (request.PaidAmount < 0)
            return Result<int>.Failure("Paid amount cannot be negative.");

        var customer = await _db.Customers.FirstOrDefaultAsync(x => x.Id == request.CustomerId && x.IsActive);
        if (customer is null)
            return Result<int>.Failure("Customer not found.");

        var vehicle = await _db.Vehicles.FirstOrDefaultAsync(x => x.Id == request.VehicleId && x.IsActive);
        if (vehicle is null)
            return Result<int>.Failure("Vehicle not found.");

        var invoiceLines = request.Items
            .Select(i => new SalesInvoiceItem
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            })
            .ToList();

        foreach (var item in invoiceLines)
        {
            if (item.Quantity <= 0)
                return Result<int>.Failure("Invoice quantities must be greater than zero.");
            if (item.UnitPrice <= 0)
                return Result<int>.Failure("Invoice unit prices must be greater than zero.");
        }

        var totalAmount = invoiceLines.Sum(x => x.LineTotal);
        if (totalAmount <= 0)
            return Result<int>.Failure("Invoice total must be greater than zero.");

        if (request.PaidAmount > totalAmount)
            return Result<int>.Failure("Paid amount cannot exceed invoice total.");

        if (request.SaleType == SaleType.Cash && request.PaidAmount != totalAmount)
            return Result<int>.Failure("Cash invoices must be fully paid.");

        var outstandingIncrease = totalAmount - request.PaidAmount;
        if (request.SaleType == SaleType.Credit &&
            customer.CreditLimit > 0 &&
            customer.OutstandingBalance + outstandingIncrease > customer.CreditLimit)
        {
            return Result<int>.Failure("Credit limit exceeded for this customer.");
        }

        var invoiceNo = GenerateInvoiceNumber();
        await using var transaction = await _db.Database.BeginTransactionAsync();
        var stockValidation = await TryReserveStockAsync(invoiceLines, invoiceNo);
        if (!stockValidation.IsSuccess)
            return Result<int>.Failure(stockValidation.Message);

        var invoice = new SalesInvoice
        {
            InvoiceNo = invoiceNo,
            InvoiceDate = DateTime.Now,
            CustomerId = request.CustomerId,
            VehicleId = request.VehicleId,
            SaleType = request.SaleType,
            PaidAmount = request.PaidAmount,
            Items = invoiceLines
        };

        invoice.TotalAmount = totalAmount;
        _db.SalesInvoices.Add(invoice);

        customer.OutstandingBalance += outstandingIncrease;

        await _db.SaveChangesAsync();
        await _auditService.LogAsync(GetActorUserId(actorUserId), "Create", "SalesInvoice", invoice.Id.ToString(), null, invoice.InvoiceNo);
        await transaction.CommitAsync();
        return Result<int>.Success(invoice.Id, invoice.InvoiceNo);
    }

    public async Task<Result> RegisterCollectionAsync(int customerId, decimal amount, string referenceNo, string notes, int? actorUserId = null)
    {
        var auth = _authorizationService.EnsureSalesPostAccess();
        if (!auth.IsSuccess)
            return Result.Failure(auth.Message);

        if (amount <= 0)
            return Result.Failure("Collection amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(referenceNo))
            return Result.Failure("Reference number is required.");

        var customer = await _db.Customers.FirstOrDefaultAsync(x => x.Id == customerId);
        if (customer is null)
            return Result.Failure("Customer not found.");

        if (customer.OutstandingBalance <= 0)
            return Result.Failure("Customer has no outstanding balance.");
        if (amount > customer.OutstandingBalance)
            return Result.Failure("Collection amount cannot exceed customer outstanding balance.");

        await using var transaction = await _db.Database.BeginTransactionAsync();

        customer.OutstandingBalance -= amount;
        _db.CollectionEntries.Add(new CollectionEntry
        {
            CustomerId = customerId,
            Amount = amount,
            ReferenceNo = referenceNo.Trim(),
            Notes = notes.Trim(),
            CollectionDate = DateTime.Now
        });

        await _db.SaveChangesAsync();
        await _auditService.LogAsync(GetActorUserId(actorUserId), "Create", "CollectionEntry", customerId.ToString(), null, $"{amount}|{referenceNo}");
        await transaction.CommitAsync();
        return Result.Success("Collection recorded.");
    }

    public async Task<List<CollectionEntry>> GetCollectionsAsync()
        => await _db.CollectionEntries
            .Include(x => x.Customer)
            .OrderByDescending(x => x.CollectionDate)
            .ThenByDescending(x => x.Id)
            .ToListAsync();

    private async Task<Result> TryReserveStockAsync(IEnumerable<SalesInvoiceItem> items, string referenceNo)
    {
        var groupedItems = items
            .GroupBy(x => x.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                Quantity = g.Sum(x => x.Quantity)
            })
            .ToList();

        foreach (var line in groupedItems)
        {
            var product = await _db.Products.FirstOrDefaultAsync(x => x.Id == line.ProductId && x.IsActive);
            if (product is null)
                return Result.Failure($"Product not found for product ID {line.ProductId}.");

            var stocks = await _db.StockBalances
                .Where(x => x.ProductId == line.ProductId && x.QuantityOnHand > 0)
                .OrderBy(x => x.WarehouseId)
                .ToListAsync();

            var available = stocks.Sum(x => x.QuantityOnHand);
            if (available < line.Quantity)
                return Result.Failure($"Insufficient stock for product {product.Code}.");

            var remaining = line.Quantity;
            foreach (var stock in stocks)
            {
                if (remaining <= 0)
                    break;

                var issued = Math.Min(stock.QuantityOnHand, remaining);
                stock.QuantityOnHand -= issued;
                remaining -= issued;

                _db.WarehouseTransactions.Add(new WarehouseTransaction
                {
                    ProductId = line.ProductId,
                    WarehouseId = stock.WarehouseId,
                    TransactionType = "SALE-OUT",
                    QuantityIn = 0,
                    QuantityOut = issued,
                    ReferenceNo = referenceNo,
                    Remarks = "Sales invoice issue"
                });
            }
        }

        return Result.Success("Stock reserved.");
    }

    private static string GenerateInvoiceNumber()
        => $"INV-{DateTime.Now:yyyyMMdd-HHmmssfff}";

    private int? GetActorUserId(int? actorUserId) => actorUserId ?? _currentUserService.CurrentUserId;
}
