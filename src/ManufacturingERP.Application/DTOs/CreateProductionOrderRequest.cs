namespace ManufacturingERP.Application.DTOs;

public class CreateProductionOrderRequest
{
    public int FinishedProductId { get; set; }
    public decimal PlannedQuantity { get; set; }
}
