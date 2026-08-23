using DeliveryTracker.API.DTOs;

namespace DeliveryTracker.API.Services;

public interface IOrderStatusService
{
    Task<OrderStatusUpdateResponse> UpdateOrderStatusAsync(int orderId, UpdateOrderStatusRequest request);
}
