namespace ManufacturingERP.Application.Services;

public class MasterDataValidationService
{
    public (bool ok, string message) ValidateRequired(string label, string? value)
        => string.IsNullOrWhiteSpace(value) ? (false, $"{label} is required.") : (true, string.Empty);

    public (bool ok, string message) ValidateNonNegative(string label, decimal value)
        => value < 0 ? (false, $"{label} cannot be negative.") : (true, string.Empty);
}
