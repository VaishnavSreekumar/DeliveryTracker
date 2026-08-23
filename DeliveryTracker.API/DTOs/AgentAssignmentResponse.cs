namespace DeliveryTracker.API.DTOs;

public class AgentAssignmentResponse
{
    public int OrderId { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public AssignedAgentDto AssignedAgent { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}
