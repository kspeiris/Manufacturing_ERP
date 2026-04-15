using ManufacturingERP.Application.Abstractions;
using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace ManufacturingERP.Application.Services;

public class UserManagementService
{
    private readonly IAppDbContext _db;
    private readonly AuditService _auditService;
    private readonly PasswordHasherService _passwordHasherService;

    public UserManagementService(IAppDbContext db, AuditService auditService, PasswordHasherService passwordHasherService)
    {
        _db = db;
        _auditService = auditService;
        _passwordHasherService = passwordHasherService;
    }

    public async Task<List<User>> GetUsersAsync()
        => await _db.Users.OrderBy(x => x.Username).ToListAsync();

    public async Task<Result<int>> SaveUserAsync(UserCrudRequest request, int actorUserId = 1)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            return Result<int>.Failure("Username is required.");

        var existingByUsername = await _db.Users.FirstOrDefaultAsync(x => x.Username == request.Username && x.Id != request.Id);
        if (existingByUsername is not null)
            return Result<int>.Failure("Username already exists.");

        if (request.Id.HasValue)
        {
            var entity = await _db.Users.FirstOrDefaultAsync(x => x.Id == request.Id.Value);
            if (entity is null) return Result<int>.Failure("User not found.");

            var oldSnapshot = $"{entity.Username}|{entity.FullName}|{entity.Role}|{entity.IsActive}";
            entity.Username = request.Username;
            if (!string.IsNullOrWhiteSpace(request.Password))
                entity.PasswordHash = _passwordHasherService.Hash(request.Password);
            entity.FullName = request.FullName;
            entity.Role = request.Role;
            entity.IsActive = request.IsActive;
            await _db.SaveChangesAsync();
            await _auditService.LogAsync(actorUserId, "Update", "User", entity.Id.ToString(), oldSnapshot, $"{entity.Username}|{entity.FullName}|{entity.Role}|{entity.IsActive}");
            return Result<int>.Success(entity.Id, "User updated.");
        }

        var user = new User
        {
            Username = request.Username,
            PasswordHash = _passwordHasherService.Hash(string.IsNullOrWhiteSpace(request.Password) ? "1234" : request.Password),
            FullName = request.FullName,
            Role = request.Role,
            IsActive = request.IsActive
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(actorUserId, "Create", "User", user.Id.ToString(), null, $"{user.Username}|{user.FullName}|{user.Role}|{user.IsActive}");
        return Result<int>.Success(user.Id, "User created.");
    }

    public async Task<Result> DeleteUserAsync(int id, int actorUserId = 1)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null) return Result.Failure("User not found.");

        var oldSnapshot = $"{user.Username}|{user.FullName}|{user.Role}|{user.IsActive}";
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(actorUserId, "Delete", "User", id.ToString(), oldSnapshot, null);
        return Result.Success("User deleted.");
    }
}
