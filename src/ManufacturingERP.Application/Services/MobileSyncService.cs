using ManufacturingERP.Application.Abstractions;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace ManufacturingERP.Application.Services;

public class MobileSyncService
{
    private readonly IAppDbContext _db;
    private readonly AuthorizationService _authorizationService;

    public MobileSyncService(IAppDbContext db, AuthorizationService authorizationService)
    {
        _db = db;
        _authorizationService = authorizationService;
    }

    public async Task<Result<int>> StartVehicleSyncAsync(string deviceId, string syncType = "VehicleSalesPush")
    {
        var auth = _authorizationService.EnsureSalesAccess();
        if (!auth.IsSuccess) return Result<int>.Failure(auth.Message);

        var log = new SyncLog
        {
            DeviceId = deviceId,
            SyncType = syncType,
            SyncStartedAt = DateTime.UtcNow,
            Status = "Completed",
            SyncCompletedAt = DateTime.UtcNow,
            Notes = "Simulated mobile sync completed."
        };
        _db.SyncLogs.Add(log);
        await _db.SaveChangesAsync();
        return Result<int>.Success(log.Id, "Sync logged.");
    }

    public async Task<List<SyncLog>> GetRecentSyncsAsync()
        => await _db.SyncLogs.OrderByDescending(x => x.SyncStartedAt).Take(100).ToListAsync();
}
