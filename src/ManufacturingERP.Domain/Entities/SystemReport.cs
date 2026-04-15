using ManufacturingERP.Domain.Common;

namespace ManufacturingERP.Domain.Entities;

public class SystemReport : BaseEntity
{
    public string ReportCode { get; set; } = string.Empty;
    public string ReportName { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty;
    public string? TemplatePath { get; set; }
    public bool IsActive { get; set; } = true;
}
