using ManufacturingERP.Domain.Entities;

namespace ManufacturingERP.Application.Services;

public class CurrentUserService
{
    public User? CurrentUser { get; private set; }

    public void Set(User user) => CurrentUser = user;
    public void Clear() => CurrentUser = null;

    public bool IsInRole(params string[] roles)
        => CurrentUser is not null && roles.Any(r => string.Equals(CurrentUser.Role.ToString(), r, StringComparison.OrdinalIgnoreCase));
}
