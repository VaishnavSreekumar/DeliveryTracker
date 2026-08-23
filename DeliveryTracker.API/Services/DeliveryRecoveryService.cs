using Microsoft.EntityFrameworkCore;
using DeliveryTracker.API.Data;
using DeliveryTracker.API.DTOs;
using DeliveryTracker.API.Entities;
using DeliveryTracker.API.Enums;

namespace DeliveryTracker.API.Services;

public class DeliveryRecoveryService : IDeliveryRecoveryService
{
    private readonly AppDbContext _context;
    private readonly IAgentAssignmentService _agentAssignmentService;

    public DeliveryRecoveryService(AppDbContext context, IAgentAssignmentService agentAssignmentService)
    {
        _context = context;
        _agentAssignmentService = agentAssignmentService;
    }

    public async Task<RescheduleOrderResponse> RescheduleOrderAsync(int orderId, RescheduleOrderRequest request)
    {
        // 1. Load Order
        var order = await _context.Orders
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
        {
            throw new KeyNotFoundException($"Order with ID {orderId} not found.");
        }

        // 2. Validate Customer existence & ownership
        var customer = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == request.CustomerId);

        if (customer == null)
        {
            throw new KeyNotFoundException($"Customer with ID {request.CustomerId} not found.");
        }

        if (order.CustomerId != request.CustomerId)
        {
            throw new UnauthorizedAccessException($"Customer ID {request.CustomerId} does not own order '{order.TrackingNumber}'.");
        }

        // 3. Validate Order status (Must be Failed)
        if (order.Status != OrderStatus.Failed)
        {
            throw new InvalidOperationException($"Cannot reschedule order '{order.TrackingNumber}' because its current status is '{order.Status}' (must be 'Failed').");
        }

        // 4. Validate RescheduledDate (Must be in future)
        if (request.RescheduledDate.Date <= DateTime.UtcNow.Date)
        {
            throw new ArgumentException("Rescheduled date must be in the future.");
        }

        var previousStatus = order.Status;
        int? previousAgentId = order.AssignedAgentId;
        AssignedAgentDto? previousAgentDto = null;
        AgentAssignmentResponse? newAssignmentResult = null;

        var executionStrategy = _context.Database.CreateExecutionStrategy();

        await executionStrategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var now = DateTime.UtcNow;

                // STEP 1 & 2: Release previous agent
                if (previousAgentId.HasValue)
                {
                    var previousAgent = await _context.Agents
                        .Include(a => a.User)
                        .Include(a => a.Zone)
                        .FirstOrDefaultAsync(a => a.Id == previousAgentId.Value);

                    if (previousAgent != null)
                    {
                        previousAgent.IsAvailable = true; // Released!
                        previousAgentDto = new AssignedAgentDto
                        {
                            Id = previousAgent.Id,
                            Name = previousAgent.User?.FullName ?? "Agent",
                            Email = previousAgent.User?.Email ?? string.Empty,
                            ZoneName = previousAgent.Zone?.Name ?? "Unknown Zone"
                        };
                    }
                }

                // STEP 3: Clear assigned agent from order
                order.AssignedAgentId = null;

                // STEP 4: Update latest DeliveryAttempt with RescheduledDate
                var latestAttempt = await _context.DeliveryAttempts
                    .Where(d => d.OrderId == order.Id)
                    .OrderByDescending(d => d.AttemptedAt)
                    .FirstOrDefaultAsync();

                if (latestAttempt != null)
                {
                    latestAttempt.RescheduledDate = request.RescheduledDate;
                }

                // STEP 5: Change order status to Rescheduled and persist the requested date
                order.Status = OrderStatus.Rescheduled;
                order.RescheduledDate = request.RescheduledDate;
                order.UpdatedAt = now;

                // STEP 6: Create OrderStatusHistory record
                var historyEntry = new OrderStatusHistory
                {
                    OrderId = order.Id,
                    Status = OrderStatus.Rescheduled,
                    ActorId = customer.Id,
                    ActorRole = UserRole.Customer,
                    Notes = request.Notes ?? $"Rescheduled for delivery on {request.RescheduledDate:yyyy-MM-dd}",
                    Timestamp = now
                };
                _context.OrderStatusHistories.Add(historyEntry);

                // STEP 7: Create customer notification 1 (Reschedule confirmation)
                var notification1 = new Notification
                {
                    UserId = customer.Id,
                    OrderId = order.Id,
                    Title = $"Delivery Rescheduled - {order.TrackingNumber}",
                    RecipientEmail = customer.Email,
                    Message = $"Order {order.TrackingNumber} has been rescheduled for {request.RescheduledDate:yyyy-MM-dd}.",
                    IsRead = false,
                    SentAt = now
                };
                _context.Notifications.Add(notification1);

                await _context.SaveChangesAsync();

                // STEP 8-11: Trigger Auto-Assignment excluding previous agent
                newAssignmentResult = await _agentAssignmentService.AutoAssignAgentAsync(order.Id, excludeAgentId: previousAgentId);

                // STEP 12: Create customer notification 2 (Reassignment confirmation)
                var notification2 = new Notification
                {
                    UserId = customer.Id,
                    OrderId = order.Id,
                    Title = $"Agent Reassigned - {order.TrackingNumber}",
                    RecipientEmail = customer.Email,
                    Message = $"Order {order.TrackingNumber} has been rescheduled and assigned to Agent {newAssignmentResult.AssignedAgent.Name}.",
                    IsRead = false,
                    SentAt = DateTime.UtcNow
                };
                _context.Notifications.Add(notification2);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();

                // Fail-safe state restoration for non-relational or transaction-ignored DB contexts
                order.Status = previousStatus;
                order.AssignedAgentId = previousAgentId;

                if (previousAgentId.HasValue)
                {
                    var prevAgent = await _context.Agents.FirstOrDefaultAsync(a => a.Id == previousAgentId.Value);
                    if (prevAgent != null)
                    {
                        prevAgent.IsAvailable = false; // Restore previous agent availability
                    }
                }

                await _context.SaveChangesAsync();

                throw;
            }
        });

        return new RescheduleOrderResponse
        {
            OrderId = order.Id,
            TrackingNumber = order.TrackingNumber,
            PreviousStatus = previousStatus,
            CurrentStatus = order.Status,
            RescheduledDate = request.RescheduledDate,
            PreviousAgent = previousAgentDto,
            NewAgent = newAssignmentResult!.AssignedAgent,
            Message = "Order rescheduled and reassigned successfully."
        };
    }
}
