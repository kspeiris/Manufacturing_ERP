using ManufacturingERP.Application.Abstractions;
using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace ManufacturingERP.Application.Services;

public class AccountingService
{
    private readonly IAppDbContext _db;

    public AccountingService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<Result<int>> CreateJournalEntryAsync(CreateJournalEntryRequest request)
    {
        if (!request.Lines.Any())
            return Result<int>.Failure("Journal entry requires at least one line.");

        var debit = request.Lines.Sum(x => x.Debit);
        var credit = request.Lines.Sum(x => x.Credit);
        if (debit != credit)
            return Result<int>.Failure("Debit and credit totals must match.");

        var entry = new JournalEntry
        {
            EntryNo = $"JV-{DateTime.Now:yyyyMMddHHmmss}",
            EntryDate = DateTime.Now,
            Description = request.Description,
            TotalDebit = debit,
            TotalCredit = credit,
            Lines = request.Lines.Select(x => new JournalLine
            {
                AccountCode = x.AccountCode,
                AccountName = x.AccountName,
                Debit = x.Debit,
                Credit = x.Credit
            }).ToList()
        };

        _db.JournalEntries.Add(entry);
        await _db.SaveChangesAsync();
        return Result<int>.Success(entry.Id, entry.EntryNo);
    }

    public async Task<List<TrialBalanceRowDto>> GetTrialBalanceAsync()
    {
        return await _db.JournalLines
            .GroupBy(x => new { x.AccountCode, x.AccountName })
            .Select(g => new TrialBalanceRowDto
            {
                AccountCode = g.Key.AccountCode,
                AccountName = g.Key.AccountName,
                Debit = g.Sum(x => x.Debit),
                Credit = g.Sum(x => x.Credit)
            })
            .OrderBy(x => x.AccountCode)
            .ToListAsync();
    }
}
