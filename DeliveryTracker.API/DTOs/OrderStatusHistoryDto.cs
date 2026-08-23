using DeliveryTracker.API.Enums;

namespace DeliveryTracker.API.DTOs;

public class OrderStatusHistoryDto
{
    public int Id { get; set; }
    public OrderStatus Status { get; set; }
    public int ActorId { get; set; }
    public UserRole ActorRole { get; set; }
    public string? Notes { get; set; }
    public DateTime Timestamp { get; set; }
}
