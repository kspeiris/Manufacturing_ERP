namespace ManufacturingERP.Application.DTOs;

public class CreateSupplierInvoiceRequest
{
    public int SupplierId { get; set; }
    public string? ReferencePoNo { get; set; }
    public string? ReferenceGrnNo { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Notes { get; set; }
}
