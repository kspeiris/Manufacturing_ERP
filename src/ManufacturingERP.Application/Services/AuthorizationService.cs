using ManufacturingERP.Shared.Results;

namespace ManufacturingERP.Application.Services;

public class AuthorizationService
{
    private readonly CurrentUserService _currentUserService;

    public AuthorizationService(CurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public Result EnsureSignedIn()
    {
        return _currentUserService.CurrentUser is null
            ? Result.Failure("You must sign in first.")
            : Result.Success("Authorized");
    }

    public Result EnsureRole(params string[] allowedRoles)
    {
        var signed = EnsureSignedIn();
        if (!signed.IsSuccess) return signed;

        var user = _currentUserService.CurrentUser!;
        if (!allowedRoles.Any(r => string.Equals(r, user.Role.ToString(), StringComparison.OrdinalIgnoreCase)))
            return Result.Failure($"Access denied for role: {user.Role}.");

        return Result.Success("Authorized");
    }

    public Result EnsureSalesAccess() => EnsureRole("Admin", "Manager", "Sales", "Accounts");
    public Result EnsureAccountingAccess() => EnsureRole("Admin", "Manager", "Accounts");
    public Result EnsureWarehouseAccess() => EnsureRole("Admin", "Manager", "Warehouse");
    public Result EnsureProductionAccess() => EnsureRole("Admin", "Manager", "Production");
    public Result EnsureAdminAccess() => EnsureRole("Admin", "Manager");
}
