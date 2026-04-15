using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Domain.Enums;
using ManufacturingERP.Shared.Results;

namespace ManufacturingERP.Application.Services;

public class PosSalesService
{
    private readonly SalesService _salesService;
    private readonly AuthorizationService _authorizationService;

    public PosSalesService(SalesService salesService, AuthorizationService authorizationService)
    {
        _salesService = salesService;
        _authorizationService = authorizationService;
    }

    public async Task<Result<int>> QuickCashSaleAsync(int customerId, int vehicleId, List<CreateInvoiceLineRequest> items, decimal paidAmount)
    {
        var auth = _authorizationService.EnsureSalesAccess();
        if (!auth.IsSuccess) return Result<int>.Failure(auth.Message);

        var request = new CreateInvoiceRequest
        {
            CustomerId = customerId,
            VehicleId = vehicleId,
            SaleType = SaleType.Cash,
            PaidAmount = paidAmount,
            Items = items
        };

        return await _salesService.CreateInvoiceAsync(request);
    }
}
