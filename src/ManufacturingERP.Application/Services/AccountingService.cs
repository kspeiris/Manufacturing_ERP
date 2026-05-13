using ManufacturingERP.Application.Abstractions;
using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Domain.Enums;
using ManufacturingERP.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace ManufacturingERP.Application.Services;

public class AccountingService
{
    private const string CashAccount = "1000";
    private const string ArAccount = "1100";
    private const string InventoryAccount = "1200";
    private const string ApAccount = "2000";
    private const string SalesAccount = "4000";
    private const string CostOfSalesAccount = "5000";
    private const string ExpenseAccount = "6000";

    private readonly IAppDbContext _db;
    private readonly AuthorizationService _authorizationService;
    private readonly AuditService _auditService;
    private readonly CurrentUserService _currentUserService;

    public AccountingService(IAppDbContext db, AuthorizationService authorizationService, AuditService auditService, CurrentUserService currentUserService)
    {
        _db = db;
        _authorizationService = authorizationService;
        _auditService = auditService;
        _currentUserService = currentUserService;
    }

    public async Task<Result<int>> CreateJournalEntryAsync(CreateJournalEntryRequest request, int? actorUserId = null)
    {
        var auth = _authorizationService.EnsureAccountingPostAccess();
        if (!auth.IsSuccess) return Result<int>.Failure(auth.Message);

        var entryDate = request.EntryDate == default ? DateTime.Today : request.EntryDate.Date;
        var periodCheck = await EnsureOpenFiscalPeriodAsync(entryDate);
        if (!periodCheck.IsSuccess) return Result<int>.Failure(periodCheck.Message);

        if (!request.Lines.Any()) return Result<int>.Failure("Journal entry requires at least one line.");
        if (request.Lines.Any(x => string.IsNullOrWhiteSpace(x.AccountCode) || x.Debit < 0 || x.Credit < 0 || (x.Debit > 0 && x.Credit > 0) || (x.Debit == 0 && x.Credit == 0)))
            return Result<int>.Failure("Each journal line must have an account and either a debit or a credit amount.");

        var accounts = await _db.Accounts.Where(x => x.IsActive).ToDictionaryAsync(x => x.AccountCode, x => x.AccountName);
        foreach (var line in request.Lines)
            if (!accounts.ContainsKey(line.AccountCode))
                return Result<int>.Failure($"Account code not found: {line.AccountCode}");

        var debit = request.Lines.Sum(x => x.Debit);
        var credit = request.Lines.Sum(x => x.Credit);
        if (debit != credit) return Result<int>.Failure("Debit and credit totals must match.");

        var entry = new JournalEntry
        {
            EntryNo = $"JV-{DateTime.Now:yyyyMMdd-HHmmssfff}",
            EntryDate = entryDate,
            Description = request.Description,
            TotalDebit = debit,
            TotalCredit = credit,
            Lines = request.Lines.Select(x => new JournalLine { AccountCode = x.AccountCode, AccountName = accounts[x.AccountCode], Debit = x.Debit, Credit = x.Credit }).ToList()
        };

        await using var transaction = await _db.Database.BeginTransactionAsync();
        _db.JournalEntries.Add(entry);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(GetActorUserId(actorUserId), "Create", "JournalEntry", entry.Id.ToString(), null, entry.EntryNo);
        await transaction.CommitAsync();
        return Result<int>.Success(entry.Id, entry.EntryNo);
    }

    public async Task<Result<int>> CreateVoucherAsync(CreateVoucherRequest request, int? actorUserId = null)
    {
        var auth = _authorizationService.EnsureAccountingPostAccess();
        if (!auth.IsSuccess) return Result<int>.Failure(auth.Message);

        var voucherDate = request.VoucherDate == default ? DateTime.Today : request.VoucherDate.Date;
        var period = await GetOpenFiscalPeriodAsync(voucherDate);
        if (period is null) return Result<int>.Failure($"No open fiscal period found for {voucherDate:yyyy-MM-dd}.");
        if (!request.Lines.Any()) return Result<int>.Failure("Voucher requires at least one line.");

        var accountCodes = request.Lines.Select(x => x.AccountCode).Distinct().ToList();
        var accounts = await _db.Accounts.Where(x => x.IsActive && accountCodes.Contains(x.AccountCode)).ToDictionaryAsync(x => x.AccountCode);
        if (accounts.Count != accountCodes.Count) return Result<int>.Failure("One or more voucher account codes are invalid.");

        var taxIds = request.Lines.Where(x => x.TaxId.HasValue).Select(x => x.TaxId!.Value).Distinct().ToList();
        var taxes = await _db.Taxes.Where(x => taxIds.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id);
        if (taxes.Count != taxIds.Count) return Result<int>.Failure("One or more voucher tax codes are invalid or inactive.");

        var lines = new List<VoucherLine>();
        foreach (var line in request.Lines)
        {
            if (line.Debit < 0 || line.Credit < 0 || (line.Debit > 0 && line.Credit > 0) || (line.Debit == 0 && line.Credit == 0))
                return Result<int>.Failure("Each voucher line must have either a debit or a credit amount.");

            var amount = line.Debit > 0 ? line.Debit : line.Credit;
            lines.Add(new VoucherLine
            {
                AccountId = accounts[line.AccountCode].Id,
                Description = line.Description,
                Debit = line.Debit,
                Credit = line.Credit,
                TaxId = line.TaxId,
                TaxAmount = line.TaxId.HasValue ? taxes[line.TaxId.Value].Calculate(amount) : 0m,
                CostCenter = line.CostCenter
            });
        }

        var voucher = new Voucher
        {
            VoucherNo = $"V-{DateTime.Now:yyyyMMdd-HHmmssfff}",
            VoucherType = request.VoucherType,
            VoucherDate = voucherDate,
            Description = request.Description,
            Reference = request.Reference,
            FiscalPeriodId = period.Id,
            TotalDebit = lines.Sum(x => x.Debit),
            TotalCredit = lines.Sum(x => x.Credit),
            CreatedByUserId = GetRequiredActorUserId(actorUserId),
            Lines = lines
        };

        if (!voucher.IsBalanced) return Result<int>.Failure("Voucher is not balanced. Total Debit must equal Total Credit.");

        _db.Vouchers.Add(voucher);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(GetActorUserId(actorUserId), "Create", "Voucher", voucher.Id.ToString(), null, voucher.VoucherNo);
        return Result<int>.Success(voucher.Id, voucher.VoucherNo);
    }

    public async Task<Result> SubmitVoucherAsync(int voucherId)
    {
        var voucher = await _db.Vouchers.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == voucherId);
        if (voucher is null) return Result.Failure("Voucher not found.");

        try
        {
            voucher.Submit();
            await _db.SaveChangesAsync();
            return Result.Success("Voucher submitted for approval.");
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public async Task<Result> ApproveVoucherAsync(int voucherId, int? actorUserId = null)
    {
        var auth = _authorizationService.EnsureAccountingPostAccess();
        if (!auth.IsSuccess) return Result.Failure(auth.Message);

        var voucher = await _db.Vouchers.FirstOrDefaultAsync(x => x.Id == voucherId);
        if (voucher is null) return Result.Failure("Voucher not found.");

        try
        {
            voucher.Approve(GetRequiredActorUserId(actorUserId));
            await _db.SaveChangesAsync();
            await _auditService.LogAsync(GetActorUserId(actorUserId), "Approve", "Voucher", voucher.Id.ToString(), null, voucher.VoucherNo);
            return Result.Success("Voucher approved.");
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public async Task<Result<int>> PostVoucherAsync(int voucherId, int? actorUserId = null)
    {
        var voucher = await _db.Vouchers.Include(x => x.Lines).ThenInclude(x => x.Account).FirstOrDefaultAsync(x => x.Id == voucherId);
        if (voucher is null) return Result<int>.Failure("Voucher not found.");

        var periodCheck = await EnsureOpenFiscalPeriodAsync(voucher.VoucherDate);
        if (!periodCheck.IsSuccess) return Result<int>.Failure(periodCheck.Message);
        if (voucher.Status != VoucherStatus.Approved) return Result<int>.Failure("Only approved vouchers can be posted.");
        if (!voucher.IsBalanced) return Result<int>.Failure("Voucher is not balanced.");

        var description = $"VOUCHER:{voucher.VoucherNo}";
        if (await _db.JournalEntries.AnyAsync(x => x.Description == description))
            return Result<int>.Failure("Voucher has already been posted.");

        var journal = new JournalEntry
        {
            EntryNo = $"JV-{DateTime.Now:yyyyMMdd-HHmmssfff}",
            EntryDate = voucher.VoucherDate.Date,
            Description = description,
            TotalDebit = voucher.TotalDebit,
            TotalCredit = voucher.TotalCredit,
            Lines = voucher.Lines.Select(x => new JournalLine
            {
                AccountCode = x.Account?.AccountCode ?? string.Empty,
                AccountName = x.Account?.AccountName ?? string.Empty,
                Debit = x.Debit,
                Credit = x.Credit
            }).ToList()
        };
        if (journal.Lines.Any(x => string.IsNullOrWhiteSpace(x.AccountCode)))
            return Result<int>.Failure("Voucher has an invalid account line.");

        await using var transaction = await _db.Database.BeginTransactionAsync();
        _db.JournalEntries.Add(journal);
        voucher.Post();
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(GetActorUserId(actorUserId), "Post", "Voucher", voucher.Id.ToString(), null, voucher.VoucherNo);
        await transaction.CommitAsync();
        return Result<int>.Success(journal.Id, journal.EntryNo);
    }

    public async Task<Result<int>> ReverseVoucherAsync(int voucherId, int? actorUserId = null)
    {
        var voucher = await _db.Vouchers.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == voucherId);
        if (voucher is null) return Result<int>.Failure("Voucher not found.");

        try
        {
            var reversal = voucher.CreateReversal(GetRequiredActorUserId(actorUserId), $"RV-{DateTime.Now:yyyyMMdd-HHmmssfff}");
            _db.Vouchers.Add(reversal);
            await _db.SaveChangesAsync();
            await _auditService.LogAsync(GetActorUserId(actorUserId), "Reverse", "Voucher", voucher.Id.ToString(), voucher.VoucherNo, reversal.VoucherNo);
            return Result<int>.Success(reversal.Id, reversal.VoucherNo);
        }
        catch (InvalidOperationException ex)
        {
            return Result<int>.Failure(ex.Message);
        }
    }

    public async Task<Result<TaxCalculationDto>> CalculateTaxAsync(int taxId, decimal baseAmountOrQty)
    {
        if (baseAmountOrQty < 0) return Result<TaxCalculationDto>.Failure("Tax base amount cannot be negative.");
        var tax = await _db.Taxes.FirstOrDefaultAsync(x => x.Id == taxId && x.IsActive);
        if (tax is null) return Result<TaxCalculationDto>.Failure("Tax not found or inactive.");

        return Result<TaxCalculationDto>.Success(new TaxCalculationDto
        {
            TaxId = tax.Id,
            TaxCode = tax.TaxCode,
            TaxName = tax.TaxName,
            TaxAmount = tax.Calculate(baseAmountOrQty)
        });
    }

    public async Task SyncSystemPostingsAsync()
    {
        await SyncSalesAsync();
        await SyncCollectionsAsync();
        await SyncSupplierInvoicesAsync();
        await SyncSupplierPaymentsAsync();
        await SyncPurchaseReturnsAsync();
        await SyncStockAdjustmentsAsync();
    }

    public async Task<List<Account>> GetAccountsAsync()
    {
        var auth = _authorizationService.EnsureAccountingAccess();
        if (!auth.IsSuccess) return [];
        return await _db.Accounts.Where(x => x.IsActive).OrderBy(x => x.AccountCode).ToListAsync();
    }

    public async Task<List<TrialBalanceRowDto>> GetTrialBalanceAsync()
    {
        var auth = _authorizationService.EnsureAccountingAccess();
        if (!auth.IsSuccess) return [];
        await SyncSystemPostingsAsync();
        var lines = await _db.JournalLines.OrderBy(x => x.AccountCode).ToListAsync();
        return lines.GroupBy(x => new { x.AccountCode, x.AccountName }).Select(g => new TrialBalanceRowDto { AccountCode = g.Key.AccountCode, AccountName = g.Key.AccountName, Debit = g.Sum(x => x.Debit), Credit = g.Sum(x => x.Credit) }).OrderBy(x => x.AccountCode).ToList();
    }

    public async Task<List<ProfitLossRowDto>> GetProfitLossAsync()
    {
        var auth = _authorizationService.EnsureAccountingAccess();
        if (!auth.IsSuccess) return [];
        var trial = await GetTrialBalanceAsync();
        return new List<ProfitLossRowDto>
        {
            new() { AccountType = "Revenue", Balance = trial.Where(x => x.AccountCode.StartsWith("4")).Sum(x => x.Credit - x.Debit) },
            new() { AccountType = "Cost of Sales", Balance = trial.Where(x => x.AccountCode.StartsWith("5")).Sum(x => x.Debit - x.Credit) },
            new() { AccountType = "Expenses", Balance = trial.Where(x => x.AccountCode.StartsWith("6")).Sum(x => x.Debit - x.Credit) }
        };
    }

    public async Task<List<BalanceSheetRowDto>> GetBalanceSheetAsync()
    {
        var auth = _authorizationService.EnsureAccountingAccess();
        if (!auth.IsSuccess) return [];
        var trial = await GetTrialBalanceAsync();
        return new List<BalanceSheetRowDto>
        {
            new() { Section = "Assets", Balance = trial.Where(x => x.AccountCode.StartsWith("1")).Sum(x => x.Debit - x.Credit) },
            new() { Section = "Liabilities", Balance = trial.Where(x => x.AccountCode.StartsWith("2")).Sum(x => x.Credit - x.Debit) },
            new() { Section = "Equity", Balance = trial.Where(x => x.AccountCode.StartsWith("3")).Sum(x => x.Credit - x.Debit) }
        };
    }

    private async Task SyncSalesAsync()
    {
        var invoices = await _db.SalesInvoices.Include(x => x.Items).ToListAsync();
        var productCosts = await _db.Products.ToDictionaryAsync(x => x.Id, x => x.CostPrice);
        foreach (var invoice in invoices)
        {
            var description = $"AUTO-SALE:{invoice.InvoiceNo}";
            if (await _db.JournalEntries.AnyAsync(x => x.Description == description)) continue;
            var cost = invoice.Items.Sum(x => x.Quantity * productCosts.GetValueOrDefault(x.ProductId));
            var lines = new List<CreateJournalLineRequest>
            {
                new() { AccountCode = invoice.PaidAmount > 0 ? CashAccount : ArAccount, Debit = invoice.PaidAmount > 0 ? invoice.PaidAmount : invoice.TotalAmount },
                new() { AccountCode = invoice.PaidAmount > 0 && invoice.PaidAmount < invoice.TotalAmount ? ArAccount : CashAccount, Debit = invoice.TotalAmount - invoice.PaidAmount },
                new() { AccountCode = SalesAccount, Credit = invoice.TotalAmount },
                new() { AccountCode = CostOfSalesAccount, Debit = cost },
                new() { AccountCode = InventoryAccount, Credit = cost }
            }.Where(x => x.Debit > 0 || x.Credit > 0).ToList();
            await CreateJournalEntryAsync(new CreateJournalEntryRequest { Description = description, Lines = lines });
        }
    }

    private async Task SyncCollectionsAsync()
    {
        var entries = await _db.CollectionEntries.ToListAsync();
        foreach (var entry in entries)
        {
            var description = $"AUTO-COLLECTION:{entry.ReferenceNo}";
            if (await _db.JournalEntries.AnyAsync(x => x.Description == description)) continue;
            await CreateJournalEntryAsync(new CreateJournalEntryRequest { Description = description, Lines = [new() { AccountCode = CashAccount, Debit = entry.Amount }, new() { AccountCode = ArAccount, Credit = entry.Amount }] });
        }
    }

    private async Task SyncSupplierInvoicesAsync()
    {
        var invoices = await _db.SupplierInvoices.ToListAsync();
        foreach (var invoice in invoices)
        {
            var description = $"AUTO-SUPPLIER-INVOICE:{invoice.InvoiceNo}";
            if (await _db.JournalEntries.AnyAsync(x => x.Description == description)) continue;
            await CreateJournalEntryAsync(new CreateJournalEntryRequest { Description = description, Lines = [new() { AccountCode = InventoryAccount, Debit = invoice.TotalAmount }, new() { AccountCode = ApAccount, Credit = invoice.TotalAmount }] });
        }
    }

    private async Task SyncSupplierPaymentsAsync()
    {
        var payments = await _db.SupplierPayments.ToListAsync();
        foreach (var payment in payments)
        {
            var description = $"AUTO-SUPPLIER-PAYMENT:{payment.PaymentNo}";
            if (await _db.JournalEntries.AnyAsync(x => x.Description == description)) continue;
            await CreateJournalEntryAsync(new CreateJournalEntryRequest { Description = description, Lines = [new() { AccountCode = ApAccount, Debit = payment.Amount }, new() { AccountCode = CashAccount, Credit = payment.Amount }] });
        }
    }

    private async Task SyncPurchaseReturnsAsync()
    {
        var returns = await _db.PurchaseReturns.ToListAsync();
        foreach (var item in returns)
        {
            var description = $"AUTO-PURCHASE-RETURN:{item.ReturnNo}";
            if (await _db.JournalEntries.AnyAsync(x => x.Description == description)) continue;
            await CreateJournalEntryAsync(new CreateJournalEntryRequest { Description = description, Lines = [new() { AccountCode = ApAccount, Debit = item.TotalAmount }, new() { AccountCode = InventoryAccount, Credit = item.TotalAmount }] });
        }
    }

    private async Task SyncStockAdjustmentsAsync()
    {
        var adjustments = await _db.WarehouseTransactions.Include(x => x.Product).Where(x => x.TransactionType == "ADJ-IN" || x.TransactionType == "ADJ-OUT").ToListAsync();
        foreach (var tx in adjustments)
        {
            var description = $"AUTO-STOCK-ADJ:{tx.ReferenceNo}";
            if (await _db.JournalEntries.AnyAsync(x => x.Description == description)) continue;
            var amount = (tx.QuantityIn > 0 ? tx.QuantityIn : tx.QuantityOut) * (tx.Product?.CostPrice ?? 0m);
            if (amount <= 0) continue;
            var lines = tx.TransactionType == "ADJ-IN"
                ? new List<CreateJournalLineRequest> { new() { AccountCode = InventoryAccount, Debit = amount }, new() { AccountCode = ExpenseAccount, Credit = amount } }
                : new List<CreateJournalLineRequest> { new() { AccountCode = ExpenseAccount, Debit = amount }, new() { AccountCode = InventoryAccount, Credit = amount } };
            await CreateJournalEntryAsync(new CreateJournalEntryRequest { Description = description, Lines = lines });
        }
    }

    private int? GetActorUserId(int? actorUserId) => actorUserId ?? _currentUserService.CurrentUserId;

    private int GetRequiredActorUserId(int? actorUserId)
        => actorUserId ?? _currentUserService.CurrentUserId ?? 0;

    private async Task<Result> EnsureOpenFiscalPeriodAsync(DateTime transactionDate)
    {
        var period = await _db.FiscalPeriods.FirstOrDefaultAsync(x => x.StartDate.Date <= transactionDate.Date && x.EndDate.Date >= transactionDate.Date);
        if (period is null) return Result.Failure($"No fiscal period found for {transactionDate:yyyy-MM-dd}.");
        if (!period.IsOpen) return Result.Failure($"Fiscal period {period.PeriodName} is {period.Status}.");
        return Result.Success();
    }

    private async Task<FiscalPeriod?> GetOpenFiscalPeriodAsync(DateTime transactionDate)
        => await _db.FiscalPeriods.FirstOrDefaultAsync(x =>
            x.StartDate.Date <= transactionDate.Date &&
            x.EndDate.Date >= transactionDate.Date &&
            x.Status == FiscalPeriodStatus.Open);
}
