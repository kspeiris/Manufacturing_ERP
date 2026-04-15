namespace ManufacturingERP.Application.DTOs;

public class CreateSupplierPaymentRequest
{
    public int SupplierId { get; set; }
    public string? ReferenceInvoiceNo { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string? Notes { get; set; }
}
