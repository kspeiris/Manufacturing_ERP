namespace ManufacturingERP.Application.DTOs;

public class StockRowDto
{
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string WarehouseName { get; set; } = string.Empty;
    public decimal QuantityOnHand { get; set; }
    public decimal CostPrice { get; set; }
    public decimal StockValue => QuantityOnHand * CostPrice;
}
