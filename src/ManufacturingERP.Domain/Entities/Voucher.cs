using ManufacturingERP.Domain.Common;
using ManufacturingERP.Domain.Enums;

namespace ManufacturingERP.Domain.Entities;

/// <summary>
/// Accounting voucher that must be approved before posting to the general ledger.
/// </summary>
public class Voucher : BaseEntity
{
    public string VoucherNo { get; set; } = string.Empty;
    public VoucherType VoucherType { get; set; }
    public VoucherStatus Status { get; set; } = VoucherStatus.Draft;
    public DateTime VoucherDate { get; set; } = DateTime.Today;
    public string Description { get; set; } = string.Empty;
    public string? Reference { get; set; }

    public int FiscalPeriodId { get; set; }
    public FiscalPeriod? FiscalPeriod { get; set; }

    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }

    public int CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public int? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }

    public int? ReversalOfVoucherId { get; set; }
    public Voucher? ReversalOfVoucher { get; set; }
    public bool IsReversed { get; set; }

    public ICollection<VoucherLine> Lines { get; set; } = new List<VoucherLine>();

    public bool IsBalanced => TotalDebit == TotalCredit;

    public void Submit()
    {
        if (Status != VoucherStatus.Draft)
            throw new InvalidOperationException("Only Draft vouchers can be submitted for approval.");
        if (!IsBalanced)
            throw new InvalidOperationException("Voucher is not balanced. Total Debit must equal Total Credit.");

        Status = VoucherStatus.PendingApproval;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Approve(int byUserId)
    {
        if (Status != VoucherStatus.PendingApproval)
            throw new InvalidOperationException("Only vouchers pending approval can be approved.");

        Status = VoucherStatus.Approved;
        ApprovedByUserId = byUserId;
        ApprovedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Post()
    {
        if (Status != VoucherStatus.Approved)
            throw new InvalidOperationException("Only approved vouchers can be posted to the ledger.");

        Status = VoucherStatus.Posted;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status == VoucherStatus.Posted || Status == VoucherStatus.Reversed)
            throw new InvalidOperationException("Posted or already-reversed vouchers cannot be cancelled.");

        Status = VoucherStatus.Cancelled;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public Voucher CreateReversal(int byUserId, string reversalVoucherNo)
    {
        if (Status != VoucherStatus.Posted)
            throw new InvalidOperationException("Only Posted vouchers can be reversed.");
        if (IsReversed)
            throw new InvalidOperationException("This voucher has already been reversed.");

        IsReversed = true;
        Status = VoucherStatus.Reversed;
        UpdatedAtUtc = DateTime.UtcNow;

        var reversal = new Voucher
        {
            VoucherNo = reversalVoucherNo,
            VoucherType = VoucherType,
            Status = VoucherStatus.Approved,
            VoucherDate = DateTime.Today,
            Description = $"Reversal of {VoucherNo}",
            FiscalPeriodId = FiscalPeriodId,
            TotalDebit = TotalCredit,
            TotalCredit = TotalDebit,
            CreatedByUserId = byUserId,
            ApprovedByUserId = byUserId,
            ApprovedAtUtc = DateTime.UtcNow,
            ReversalOfVoucherId = Id
        };

        foreach (var line in Lines)
        {
            reversal.Lines.Add(new VoucherLine
            {
                AccountId = line.AccountId,
                Description = $"Reversal - {line.Description}",
                Debit = line.Credit,
                Credit = line.Debit,
                TaxId = line.TaxId,
                CostCenter = line.CostCenter
            });
        }

        return reversal;
    }
}
