using DeliveryTracker.API.DTOs;

namespace DeliveryTracker.API.Services;

public interface IDeliveryRecoveryService
{
    Task<RescheduleOrderResponse> RescheduleOrderAsync(int orderId, RescheduleOrderRequest request);
}
