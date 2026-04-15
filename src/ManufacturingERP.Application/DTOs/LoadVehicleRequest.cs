namespace ManufacturingERP.Application.DTOs;

public class LoadVehicleRequest
{
    public int VehicleId { get; set; }
    public string RouteName { get; set; } = string.Empty;
    public DateTime LoadDate { get; set; } = DateTime.Today;
    public List<LoadVehicleItemRequest> Items { get; set; } = new();
}

public class LoadVehicleItemRequest
{
    public int ProductId { get; set; }
    public decimal Quantity { get; set; }
}
