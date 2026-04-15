using ManufacturingERP.Application.Abstractions;
using ManufacturingERP.Application.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ManufacturingERP.Application.Services;

public class Phase7AccountingService
{
    private readonly IAppDbContext _db;

    public Phase7AccountingService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<List<ProfitLossRowDto>> GetProfitLossAsync()
    {
        var lines = await _db.JournalLines.ToListAsync();
        return new List<ProfitLossRowDto>
        {
            new() { AccountType = "Revenue", Balance = lines.Where(x => x.AccountCode.StartsWith("4")).Sum(x => x.Credit - x.Debit) },
            new() { AccountType = "Cost of Sales", Balance = lines.Where(x => x.AccountCode.StartsWith("5")).Sum(x => x.Debit - x.Credit) },
            new() { AccountType = "Expenses", Balance = lines.Where(x => x.AccountCode.StartsWith("6")).Sum(x => x.Debit - x.Credit) }
        };
    }

    public async Task<List<BalanceSheetRowDto>> GetBalanceSheetAsync()
    {
        var lines = await _db.JournalLines.ToListAsync();
        return new List<BalanceSheetRowDto>
        {
            new() { Section = "Assets", Balance = lines.Where(x => x.AccountCode.StartsWith("1")).Sum(x => x.Debit - x.Credit) },
            new() { Section = "Liabilities", Balance = lines.Where(x => x.AccountCode.StartsWith("2")).Sum(x => x.Credit - x.Debit) },
            new() { Section = "Equity", Balance = lines.Where(x => x.AccountCode.StartsWith("3")).Sum(x => x.Credit - x.Debit) }
        };
    }
}
