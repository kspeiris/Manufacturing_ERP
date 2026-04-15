namespace ManufacturingERP.Application.DTOs;

public class PrintReportRequest
{
    public string ReportName { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public string? FilterText { get; set; }
}
