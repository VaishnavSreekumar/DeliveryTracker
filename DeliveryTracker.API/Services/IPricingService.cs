using DeliveryTracker.API.DTOs;

namespace DeliveryTracker.API.Services;

public interface IPricingService
{
    Task<PriceCalculationResult> CalculatePriceAsync(CalculatePriceRequest request);
}
