using ManufacturingERP.Application.Abstractions;
using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Domain.Entities;
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
        if (!request.Lines.Any()) return Result<int>.Failure("Journal entry requires at least one line.");
        if (request.Lines.Any(x => string.IsNullOrWhiteSpace(x.AccountCode) || x.Debit < 0 || x.Credit < 0 || (x.Debit > 0 && x.Credit > 0) || (x.Debit == 0 && x.Credit == 0))) return Result<int>.Failure("Each journal line must have an account and either a debit or a credit amount.");
        var accounts = await _db.Accounts.Where(x => x.IsActive).ToDictionaryAsync(x => x.AccountCode, x => x.AccountName);
        foreach (var line in request.Lines) if (!accounts.ContainsKey(line.AccountCode)) return Result<int>.Failure($"Account code not found: {line.AccountCode}");
        var debit = request.Lines.Sum(x => x.Debit); var credit = request.Lines.Sum(x => x.Credit);
        if (debit != credit) return Result<int>.Failure("Debit and credit totals must match.");
        var entry = new JournalEntry
        {
            EntryNo = $"JV-{DateTime.Now:yyyyMMdd-HHmmssfff}",
            EntryDate = DateTime.Now,
            Description = request.Description,
            TotalDebit = debit,
            TotalCredit = credit,
            Lines = request.Lines.Select(x => new JournalLine { AccountCode = x.AccountCode, AccountName = accounts[x.AccountCode], Debit = x.Debit, Credit = x.Credit }).ToList()
        };
        _db.JournalEntries.Add(entry);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(GetActorUserId(actorUserId), "Create", "JournalEntry", entry.Id.ToString(), null, entry.EntryNo);
        return Result<int>.Success(entry.Id, entry.EntryNo);
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
            await CreateJournalEntryAsync(new CreateJournalEntryRequest { Description = description, Lines = [ new() { AccountCode = CashAccount, Debit = entry.Amount }, new() { AccountCode = ArAccount, Credit = entry.Amount } ] });
        }
    }

    private async Task SyncSupplierInvoicesAsync()
    {
        var invoices = await _db.SupplierInvoices.ToListAsync();
        foreach (var invoice in invoices)
        {
            var description = $"AUTO-SUPPLIER-INVOICE:{invoice.InvoiceNo}";
            if (await _db.JournalEntries.AnyAsync(x => x.Description == description)) continue;
            await CreateJournalEntryAsync(new CreateJournalEntryRequest { Description = description, Lines = [ new() { AccountCode = InventoryAccount, Debit = invoice.TotalAmount }, new() { AccountCode = ApAccount, Credit = invoice.TotalAmount } ] });
        }
    }

    private async Task SyncSupplierPaymentsAsync()
    {
        var payments = await _db.SupplierPayments.ToListAsync();
        foreach (var payment in payments)
        {
            var description = $"AUTO-SUPPLIER-PAYMENT:{payment.PaymentNo}";
            if (await _db.JournalEntries.AnyAsync(x => x.Description == description)) continue;
            await CreateJournalEntryAsync(new CreateJournalEntryRequest { Description = description, Lines = [ new() { AccountCode = ApAccount, Debit = payment.Amount }, new() { AccountCode = CashAccount, Credit = payment.Amount } ] });
        }
    }

    private async Task SyncPurchaseReturnsAsync()
    {
        var returns = await _db.PurchaseReturns.ToListAsync();
        foreach (var item in returns)
        {
            var description = $"AUTO-PURCHASE-RETURN:{item.ReturnNo}";
            if (await _db.JournalEntries.AnyAsync(x => x.Description == description)) continue;
            await CreateJournalEntryAsync(new CreateJournalEntryRequest { Description = description, Lines = [ new() { AccountCode = ApAccount, Debit = item.TotalAmount }, new() { AccountCode = InventoryAccount, Credit = item.TotalAmount } ] });
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
}
