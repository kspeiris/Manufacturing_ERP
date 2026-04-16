using ManufacturingERP.Domain.Enums;
using ManufacturingERP.Shared.Results;

namespace ManufacturingERP.Application.Services;

public class AuthorizationService
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, UserRole[]>> PermissionMatrix =
        new Dictionary<string, IReadOnlyDictionary<string, UserRole[]>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Sales"] = new Dictionary<string, UserRole[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["View"] = [UserRole.Admin, UserRole.Manager, UserRole.Sales, UserRole.Accounts],
                ["Write"] = [UserRole.Admin, UserRole.Manager, UserRole.Sales],
                ["Post"] = [UserRole.Admin, UserRole.Manager, UserRole.Sales, UserRole.Accounts]
            },
            ["Accounting"] = new Dictionary<string, UserRole[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["View"] = [UserRole.Admin, UserRole.Manager, UserRole.Accounts],
                ["Post"] = [UserRole.Admin, UserRole.Manager, UserRole.Accounts]
            },
            ["Ledgers"] = new Dictionary<string, UserRole[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["View"] = [UserRole.Admin, UserRole.Manager, UserRole.Accounts]
            },
            ["Warehouse"] = new Dictionary<string, UserRole[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["View"] = [UserRole.Admin, UserRole.Manager, UserRole.Warehouse],
                ["Post"] = [UserRole.Admin, UserRole.Manager, UserRole.Warehouse]
            },
            ["Procurement"] = new Dictionary<string, UserRole[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["View"] = [UserRole.Admin, UserRole.Manager, UserRole.Accounts, UserRole.Warehouse],
                ["Post"] = [UserRole.Admin, UserRole.Manager, UserRole.Accounts, UserRole.Warehouse]
            },
            ["Production"] = new Dictionary<string, UserRole[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["View"] = [UserRole.Admin, UserRole.Manager, UserRole.Production],
                ["Post"] = [UserRole.Admin, UserRole.Manager, UserRole.Production]
            },
            ["Admin"] = new Dictionary<string, UserRole[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["View"] = [UserRole.Admin, UserRole.Manager],
                ["Write"] = [UserRole.Admin]
            },
            ["Reports"] = new Dictionary<string, UserRole[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["View"] = [UserRole.Admin, UserRole.Manager, UserRole.Accounts, UserRole.Sales, UserRole.Warehouse, UserRole.Production]
            }
        };

    private readonly CurrentUserService _currentUserService;

    public AuthorizationService(CurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public Result EnsureSignedIn()
    {
        if (_currentUserService.CurrentUser is null)
            return Result.Failure("You must sign in first.");

        if (!_currentUserService.CurrentUser.IsActive)
        {
            _currentUserService.Clear();
            return Result.Failure("This user account is disabled.");
        }

        if (_currentUserService.HasTimedOut())
        {
            _currentUserService.Clear();
            return Result.Failure("Session timed out. Please sign in again.");
        }

        _currentUserService.Touch();
        return Result.Success("Authorized");
    }

    public Result EnsureRole(params string[] allowedRoles)
    {
        var signed = EnsureSignedIn();
        if (!signed.IsSuccess)
            return signed;

        var user = _currentUserService.CurrentUser!;
        if (!allowedRoles.Any(r => string.Equals(r, user.Role.ToString(), StringComparison.OrdinalIgnoreCase)))
            return Result.Failure($"Access denied for role: {user.Role}.");

        return Result.Success("Authorized");
    }

    public Result EnsurePermission(string moduleKey, string actionKey)
    {
        var signed = EnsureSignedIn();
        if (!signed.IsSuccess)
            return signed;

        var user = _currentUserService.CurrentUser!;
        if (user.Role == UserRole.Admin)
            return Result.Success("Authorized");

        if (!PermissionMatrix.TryGetValue(moduleKey, out var modulePermissions) ||
            !modulePermissions.TryGetValue(actionKey, out var allowedRoles))
        {
            return Result.Failure($"Permission rule is not configured for {moduleKey} {actionKey}.");
        }

        if (!allowedRoles.Contains(user.Role))
            return Result.Failure($"Access denied. {user.Role} cannot {actionKey.ToLowerInvariant()} {moduleKey.ToLowerInvariant()} actions.");

        return Result.Success("Authorized");
    }

    public Result EnsureSalesAccess() => EnsurePermission("Sales", "View");
    public Result EnsureSalesWriteAccess() => EnsurePermission("Sales", "Write");
    public Result EnsureSalesPostAccess() => EnsurePermission("Sales", "Post");
    public Result EnsureAccountingAccess() => EnsurePermission("Accounting", "View");
    public Result EnsureAccountingPostAccess() => EnsurePermission("Accounting", "Post");
    public Result EnsureWarehouseAccess() => EnsurePermission("Warehouse", "View");
    public Result EnsureWarehousePostAccess() => EnsurePermission("Warehouse", "Post");
    public Result EnsureProcurementAccess() => EnsurePermission("Procurement", "View");
    public Result EnsureProcurementPostAccess() => EnsurePermission("Procurement", "Post");
    public Result EnsureProductionAccess() => EnsurePermission("Production", "View");
    public Result EnsureProductionPostAccess() => EnsurePermission("Production", "Post");
    public Result EnsureLedgersAccess() => EnsurePermission("Ledgers", "View");
    public Result EnsureAdminAccess() => EnsurePermission("Admin", "View");
    public Result EnsureAdminWriteAccess() => EnsurePermission("Admin", "Write");
    public Result EnsureReportsAccess() => EnsurePermission("Reports", "View");
}
