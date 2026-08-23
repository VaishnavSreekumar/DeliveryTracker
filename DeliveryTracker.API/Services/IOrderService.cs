using DeliveryTracker.API.DTOs;

namespace DeliveryTracker.API.Services;

public interface IOrderService
{
    Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request);
    Task<OrderResponse?> GetOrderByIdAsync(int id);
    Task<IEnumerable<OrderSummaryResponse>> GetOrdersAsync(int? customerId = null);
}
