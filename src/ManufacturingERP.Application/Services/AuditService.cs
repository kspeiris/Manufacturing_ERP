using ManufacturingERP.Application.Abstractions;
using ManufacturingERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ManufacturingERP.Application.Services;

public class AuditService
{
    private readonly IAppDbContext _db;

    public AuditService(IAppDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(int? userId, string action, string entityName, string entityKey, string? oldValues, string? newValues)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityName = entityName,
            EntityKey = entityKey,
            OldValues = oldValues,
            NewValues = newValues,
            ActionAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    public async Task<List<AuditLog>> GetRecentAsync()
        => await _db.AuditLogs.Include(x => x.User).OrderByDescending(x => x.ActionAtUtc).Take(200).ToListAsync();
}
