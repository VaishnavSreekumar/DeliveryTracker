using DeliveryTracker.API.Enums;

namespace DeliveryTracker.API.Entities;

public class OrderStatusHistory
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public OrderStatus Status { get; set; }

    public int ActorId { get; set; }
    public UserRole ActorRole { get; set; }

    public string? Notes { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
