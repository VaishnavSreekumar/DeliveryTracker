using DeliveryTracker.API.DTOs;
using DeliveryTracker.API.Enums;

namespace DeliveryTracker.API.Services;

public interface IOrderService
{
    Task<OrderResponse> CreateOrderAsync(CreateOrderRequest request, int? creatorUserId = null, UserRole? creatorRole = null);
    Task<OrderResponse?> GetOrderByIdAsync(int id);
    Task<IEnumerable<OrderSummaryResponse>> GetOrdersAsync(
        int? customerId = null,
        OrderStatus? status = null,
        int? zoneId = null,
        int? agentId = null,
        string? search = null);
}
