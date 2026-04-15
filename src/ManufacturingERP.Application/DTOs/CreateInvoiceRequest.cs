using ManufacturingERP.Domain.Enums;

namespace ManufacturingERP.Application.DTOs;

public class CreateInvoiceRequest
{
    public int CustomerId { get; set; }
    public int VehicleId { get; set; }
    public SaleType SaleType { get; set; }
    public decimal PaidAmount { get; set; }
    public List<CreateInvoiceLineRequest> Items { get; set; } = new();
}

public class CreateInvoiceLineRequest
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
