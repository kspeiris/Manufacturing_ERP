using ManufacturingERP.Domain.Entities;

namespace ManufacturingERP.Application.Services;

public class CurrentUserService
{
    public static readonly TimeSpan DefaultSessionTimeout = TimeSpan.FromMinutes(30);

    public User? CurrentUser { get; private set; }
    public DateTime? SignedInAtUtc { get; private set; }
    public DateTime? LastActivityAtUtc { get; private set; }
    public TimeSpan SessionTimeout { get; private set; } = DefaultSessionTimeout;
    public int? CurrentUserId => CurrentUser?.Id;

    public void Set(User user, TimeSpan? sessionTimeout = null)
    {
        CurrentUser = user;
        SessionTimeout = sessionTimeout ?? DefaultSessionTimeout;
        SignedInAtUtc = DateTime.UtcNow;
        LastActivityAtUtc = SignedInAtUtc;
    }

    public void Touch()
    {
        if (CurrentUser is not null)
            LastActivityAtUtc = DateTime.UtcNow;
    }

    public bool HasTimedOut()
    {
        return CurrentUser is not null
            && LastActivityAtUtc.HasValue
            && DateTime.UtcNow - LastActivityAtUtc.Value > SessionTimeout;
    }

    public void Clear()
    {
        CurrentUser = null;
        SignedInAtUtc = null;
        LastActivityAtUtc = null;
        SessionTimeout = DefaultSessionTimeout;
    }

    public bool IsInRole(params string[] roles)
        => CurrentUser is not null && roles.Any(r => string.Equals(CurrentUser.Role.ToString(), r, StringComparison.OrdinalIgnoreCase));
}
