using ManufacturingERP.Application.Abstractions;
using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace ManufacturingERP.Application.Services;

public class UserManagementService
{
    private readonly IAppDbContext _db;
    private readonly AuthorizationService _authorizationService;
    private readonly AuditService _auditService;
    private readonly CurrentUserService _currentUserService;
    private readonly PasswordHasherService _passwordHasherService;

    public UserManagementService(
        IAppDbContext db,
        AuthorizationService authorizationService,
        AuditService auditService,
        CurrentUserService currentUserService,
        PasswordHasherService passwordHasherService)
    {
        _db = db;
        _authorizationService = authorizationService;
        _auditService = auditService;
        _currentUserService = currentUserService;
        _passwordHasherService = passwordHasherService;
    }

    public async Task<List<User>> GetUsersAsync()
    {
        var auth = _authorizationService.EnsureAdminAccess();
        if (!auth.IsSuccess)
            return [];

        return await _db.Users.OrderBy(x => x.Username).ToListAsync();
    }

    public async Task<Result<int>> SaveUserAsync(UserCrudRequest request, int? actorUserId = null)
    {
        var auth = _authorizationService.EnsureAdminWriteAccess();
        if (!auth.IsSuccess)
            return Result<int>.Failure(auth.Message);

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
            if (_currentUserService.CurrentUserId == entity.Id && !entity.IsActive)
                return Result<int>.Failure("You cannot disable the currently signed-in user.");
            await _db.SaveChangesAsync();
            await _auditService.LogAsync(GetActorUserId(actorUserId), "Update", "User", entity.Id.ToString(), oldSnapshot, $"{entity.Username}|{entity.FullName}|{entity.Role}|{entity.IsActive}");
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
        await _auditService.LogAsync(GetActorUserId(actorUserId), "Create", "User", user.Id.ToString(), null, $"{user.Username}|{user.FullName}|{user.Role}|{user.IsActive}");
        return Result<int>.Success(user.Id, "User created.");
    }

    public async Task<Result> DeleteUserAsync(int id, int? actorUserId = null)
    {
        var auth = _authorizationService.EnsureAdminWriteAccess();
        if (!auth.IsSuccess)
            return Result.Failure(auth.Message);

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (user is null) return Result.Failure("User not found.");
        if (_currentUserService.CurrentUserId == id)
            return Result.Failure("You cannot delete the currently signed-in user.");

        var oldSnapshot = $"{user.Username}|{user.FullName}|{user.Role}|{user.IsActive}";
        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(GetActorUserId(actorUserId), "Delete", "User", id.ToString(), oldSnapshot, null);
        return Result.Success("User deleted.");
    }

    public async Task<Result> ResetPasswordAsync(int userId, string newPassword, int? actorUserId = null)
    {
        var auth = _authorizationService.EnsureAdminWriteAccess();
        if (!auth.IsSuccess)
            return Result.Failure(auth.Message);
        if (string.IsNullOrWhiteSpace(newPassword))
            return Result.Failure("New password is required.");

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
            return Result.Failure("User not found.");

        var oldSnapshot = $"{user.Username}|PasswordReset";
        user.PasswordHash = _passwordHasherService.Hash(newPassword);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(GetActorUserId(actorUserId), "ResetPassword", "User", user.Id.ToString(), oldSnapshot, $"{user.Username}|PasswordReset");
        return Result.Success("Password reset.");
    }

    public async Task<Result> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        var signed = _authorizationService.EnsureSignedIn();
        if (!signed.IsSuccess)
            return signed;
        if (string.IsNullOrWhiteSpace(currentPassword))
            return Result.Failure("Current password is required.");
        if (string.IsNullOrWhiteSpace(newPassword))
            return Result.Failure("New password is required.");

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == _currentUserService.CurrentUserId);
        if (user is null)
            return Result.Failure("User not found.");
        if (!_passwordHasherService.Verify(currentPassword, user.PasswordHash))
            return Result.Failure("Current password is incorrect.");

        user.PasswordHash = _passwordHasherService.Hash(newPassword);
        await _db.SaveChangesAsync();
        await _auditService.LogAsync(user.Id, "ChangePassword", "User", user.Id.ToString(), $"{user.Username}|PasswordChanged", $"{user.Username}|PasswordChanged");
        return Result.Success("Password changed.");
    }

    private int? GetActorUserId(int? actorUserId) => actorUserId ?? _currentUserService.CurrentUserId;
}
