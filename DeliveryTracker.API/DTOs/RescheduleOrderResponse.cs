using DeliveryTracker.API.Enums;

namespace DeliveryTracker.API.DTOs;

public class RescheduleOrderResponse
{
    public int OrderId { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public OrderStatus PreviousStatus { get; set; }
    public OrderStatus CurrentStatus { get; set; }
    public DateTime RescheduledDate { get; set; }
    public AssignedAgentDto? PreviousAgent { get; set; }
    public AssignedAgentDto NewAgent { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}
