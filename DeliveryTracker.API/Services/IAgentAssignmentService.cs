using DeliveryTracker.API.DTOs;

namespace DeliveryTracker.API.Services;

public interface IAgentAssignmentService
{
    Task<AgentAssignmentResponse> AutoAssignAgentAsync(int orderId, int? excludeAgentId = null);
    Task<AgentAssignmentResponse> ManualAssignAgentAsync(int orderId, int agentId, int adminUserId);
}
