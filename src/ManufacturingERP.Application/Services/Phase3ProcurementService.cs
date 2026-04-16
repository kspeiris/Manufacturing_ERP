using ManufacturingERP.Application.Abstractions;
using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace ManufacturingERP.Application.Services;

public class Phase3ProcurementService
{
    private readonly IAppDbContext _db;
    private readonly AuthorizationService _authorizationService;
    private readonly AuditService _auditService;
    private readonly CurrentUserService _currentUserService;

    public Phase3ProcurementService(IAppDbContext db, AuthorizationService authorizationService, AuditService auditService, CurrentUserService currentUserService)
    {
        _db = db;
        _authorizationService = authorizationService;
        _auditService = auditService;
        _currentUserService = currentUserService;
    }

    public async Task<Result<int>> CreateSupplierInvoiceAsync(CreateSupplierInvoiceRequest request, int? actorUserId = null)
    {
        var auth = _authorizationService.EnsureProcurementPostAccess();
        if (!auth.IsSuccess)
            return Result<int>.Failure(auth.Message);

        var supplier = await _db.Suppliers.FirstOrDefaultAsync(x => x.Id == request.SupplierId);
        if (supplier is null) return Result<int>.Failure("Supplier not found.");

        if (request.TotalAmount <= 0)
            return Result<int>.Failure("Supplier invoice amount must be greater than zero.");

        if (request.PaidAmount < 0)
            return Result<int>.Failure("Paid amount cannot be negative.");

        if (request.PaidAmount > request.TotalAmount)
            return Result<int>.Failure("Paid amount cannot exceed invoice amount.");

        PurchaseOrder? purchaseOrder = null;
        if (!string.IsNullOrWhiteSpace(request.ReferencePoNo))
        {
            purchaseOrder = await _db.PurchaseOrders.FirstOrDefaultAsync(x => x.OrderNo == request.ReferencePoNo);
            if (purchaseOrder is null)
                return Result<int>.Failure("Referenced purchase order was not found.");

            if (purchaseOrder.SupplierId != request.SupplierId)
                return Result<int>.Failure("Purchase order supplier does not match the invoice supplier.");
        }

        GoodsReceipt? goodsReceipt = null;
        if (!string.IsNullOrWhiteSpace(request.ReferenceGrnNo))
        {
            goodsReceipt = await _db.GoodsReceipts.FirstOrDefaultAsync(x => x.ReceiptNo == request.ReferenceGrnNo);
            if (goodsReceipt is null)
                return Result<int>.Failure("Referenced goods receipt was not found.");

            if (goodsReceipt.SupplierId != request.SupplierId)
                return Result<int>.Failure("Goods receipt supplier does not match the invoice supplier.");

            if (purchaseOrder is not null && !string.Equals(goodsReceipt.PurchaseOrderNo, purchaseOrder.OrderNo, StringComparison.OrdinalIgnoreCase))
                return Result<int>.Failure("Goods receipt does not belong to the selected purchase order.");
        }

        var invoice = new SupplierInvoice
        {
            InvoiceNo = $"SINV-{DateTime.Now:yyyyMMdd-HHmmssfff}",
            InvoiceDate = DateTime.Now,
            SupplierId = request.SupplierId,
            ReferencePoNo = purchaseOrder?.OrderNo ?? request.ReferencePoNo,
            ReferenceGrnNo = goodsReceipt?.ReceiptNo ?? request.ReferenceGrnNo,
            TotalAmount = request.TotalAmount,
            PaidAmount = request.PaidAmount,
            BalanceAmount = request.TotalAmount - request.PaidAmount,
            DueDate = request.DueDate,
            Notes = request.Notes,
            Status = request.TotalAmount - request.PaidAmount <= 0 ? "Paid" : request.PaidAmount > 0 ? "Partially Paid" : "Open"
        };

        _db.SupplierInvoices.Add(invoice);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(GetActorUserId(actorUserId), "Create", "SupplierInvoice", invoice.Id.ToString(), null, invoice.InvoiceNo);
        return Result<int>.Success(invoice.Id, invoice.InvoiceNo);
    }

    public async Task<Result<int>> CreatePurchaseReturnAsync(CreatePurchaseReturnRequest request, int? actorUserId = null)
    {
        var auth = _authorizationService.EnsureProcurementPostAccess();
        if (!auth.IsSuccess)
            return Result<int>.Failure(auth.Message);

        if (!request.Items.Any())
            return Result<int>.Failure("Purchase return requires at least one line.");

        var supplier = await _db.Suppliers.FirstOrDefaultAsync(x => x.Id == request.SupplierId);
        if (supplier is null)
            return Result<int>.Failure("Supplier not found.");

        var warehouse = await _db.Warehouses.FirstOrDefaultAsync(x => x.Id == request.WarehouseId && x.IsActive);
        if (warehouse is null)
            return Result<int>.Failure("Warehouse not found or inactive.");

        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result<int>.Failure("Return reason is required.");

        var invalidLine = request.Items.FirstOrDefault(x => x.ProductId <= 0 || x.Quantity <= 0 || x.UnitCost <= 0);
        if (invalidLine is not null)
            return Result<int>.Failure("Purchase return quantities and unit costs must be greater than zero.");

        SupplierInvoice? invoice = null;
        if (!string.IsNullOrWhiteSpace(request.ReferenceInvoiceNo))
        {
            invoice = await _db.SupplierInvoices.FirstOrDefaultAsync(x => x.InvoiceNo == request.ReferenceInvoiceNo);
            if (invoice is null)
                return Result<int>.Failure("Referenced supplier invoice was not found.");

            if (invoice.SupplierId != request.SupplierId)
                return Result<int>.Failure("Supplier invoice does not belong to the selected supplier.");
        }

        var entity = new PurchaseReturn
        {
            ReturnNo = $"PRTN-{DateTime.Now:yyyyMMdd-HHmmssfff}",
            ReturnDate = DateTime.Now,
            SupplierId = request.SupplierId,
            WarehouseId = request.WarehouseId,
            ReferenceInvoiceNo = request.ReferenceInvoiceNo,
            Reason = request.Reason,
            Items = request.Items.Select(x => new PurchaseReturnItem
            {
                ProductId = x.ProductId,
                Quantity = x.Quantity,
                UnitCost = x.UnitCost
            }).ToList()
        };
        entity.TotalAmount = entity.Items.Sum(x => x.LineTotal);

        foreach (var line in request.Items)
        {
            var stock = await _db.StockBalances.FirstOrDefaultAsync(x => x.ProductId == line.ProductId && x.WarehouseId == request.WarehouseId);
            if (stock is null || stock.QuantityOnHand < line.Quantity)
                return Result<int>.Failure($"Insufficient stock for product ID {line.ProductId}.");

            stock.QuantityOnHand -= line.Quantity;
            _db.WarehouseTransactions.Add(new WarehouseTransaction
            {
                ProductId = line.ProductId,
                WarehouseId = request.WarehouseId,
                TransactionType = "PUR-RETURN",
                QuantityIn = 0,
                QuantityOut = line.Quantity,
                ReferenceNo = entity.ReturnNo,
                Remarks = request.Reason
            });
        }

        _db.PurchaseReturns.Add(entity);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(GetActorUserId(actorUserId), "Create", "PurchaseReturn", entity.Id.ToString(), null, entity.ReturnNo);
        return Result<int>.Success(entity.Id, entity.ReturnNo);
    }

    public async Task<List<SupplierInvoice>> GetSupplierInvoicesAsync()
        => await _db.SupplierInvoices.Include(x => x.Supplier).OrderByDescending(x => x.InvoiceDate).ToListAsync();

    public async Task<List<PurchaseReturn>> GetPurchaseReturnsAsync()
        => await _db.PurchaseReturns.Include(x => x.Supplier).Include(x => x.Warehouse).OrderByDescending(x => x.ReturnDate).ToListAsync();

    private int? GetActorUserId(int? actorUserId) => actorUserId ?? _currentUserService.CurrentUserId;
}
