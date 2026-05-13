using ManufacturingERP.Application.Abstractions;
using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace ManufacturingERP.Application.Services;

public class SupplierPaymentService
{
    private readonly IAppDbContext _db;
    private readonly AuditService _auditService;
    private readonly AuthorizationService _authorizationService;

    public SupplierPaymentService(IAppDbContext db, AuditService auditService, AuthorizationService authorizationService)
    {
        _db = db;
        _auditService = auditService;
        _authorizationService = authorizationService;
    }

    public async Task<Result<int>> SaveAsync(CreateSupplierPaymentRequest request, int actorUserId = 1)
    {
        var auth = _authorizationService.EnsureRole("Admin", "Manager", "Accounts");
        if (!auth.IsSuccess) return Result<int>.Failure(auth.Message);

        if (request.Amount <= 0)
            return Result<int>.Failure("Payment amount must be greater than zero.");

        var supplier = await _db.Suppliers.FirstOrDefaultAsync(x => x.Id == request.SupplierId);
        if (supplier is null)
            return Result<int>.Failure("Supplier not found.");

        SupplierInvoice? invoice = null;
        if (!string.IsNullOrWhiteSpace(request.ReferenceInvoiceNo))
        {
            invoice = await _db.SupplierInvoices.FirstOrDefaultAsync(x => x.InvoiceNo == request.ReferenceInvoiceNo && x.SupplierId == request.SupplierId);
            if (invoice is null)
                return Result<int>.Failure("Referenced supplier invoice was not found.");

            if (invoice.BalanceAmount <= 0)
                return Result<int>.Failure("Referenced supplier invoice is already fully paid.");

            if (request.Amount > invoice.BalanceAmount)
                return Result<int>.Failure("Payment amount cannot exceed the invoice balance.");
        }

        var payment = new SupplierPayment
        {
            PaymentNo = $"SPAY-{DateTime.Now:yyyyMMdd-HHmmssfff}",
            PaymentDate = DateTime.Now,
            SupplierId = request.SupplierId,
            ReferenceInvoiceNo = request.ReferenceInvoiceNo,
            Amount = request.Amount,
            PaymentMethod = request.PaymentMethod,
            Notes = request.Notes
        };

        await using var transaction = await _db.Database.BeginTransactionAsync();
        _db.SupplierPayments.Add(payment);

        if (invoice is not null)
        {
            invoice.PaidAmount += request.Amount;
            invoice.BalanceAmount = Math.Max(0, invoice.TotalAmount - invoice.PaidAmount);
            invoice.Status = invoice.BalanceAmount <= 0 ? "Paid" : "Partially Paid";
        }

        await _db.SaveChangesAsync();
        await _auditService.LogAsync(actorUserId, "Create", "SupplierPayment", payment.Id.ToString(), null, payment.PaymentNo);
        await transaction.CommitAsync();
        return Result<int>.Success(payment.Id, payment.PaymentNo);
    }

    public async Task<List<SupplierPayment>> GetRecentAsync()
        => await _db.SupplierPayments.Include(x => x.Supplier).OrderByDescending(x => x.PaymentDate).ToListAsync();
}
