using DeliveryTracker.API.Enums;

namespace DeliveryTracker.API.DTOs;

public class OrderStatusUpdateResponse
{
    public int OrderId { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public OrderStatus PreviousStatus { get; set; }
    public OrderStatus CurrentStatus { get; set; }
    public DateTime UpdatedAt { get; set; }
    public OrderStatusHistoryDto HistoryEntry { get; set; } = new();
}
