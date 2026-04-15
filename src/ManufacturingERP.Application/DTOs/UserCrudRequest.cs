using ManufacturingERP.Domain.Enums;

namespace ManufacturingERP.Application.DTOs;

public class UserCrudRequest
{
    public int? Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
}
