using DeliveryTracker.API.Enums;

namespace DeliveryTracker.API.DTOs;

public class OrderSummaryResponse
{
    public int Id { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string PickupArea { get; set; } = string.Empty;
    public string DropArea { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; }
    public int? AssignedAgentId { get; set; }
    public string? AssignedAgentName { get; set; }
    public DateTime CreatedAt { get; set; }
}
