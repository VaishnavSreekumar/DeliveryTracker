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
    private readonly INotificationService? _notificationService;

    public DeliveryRecoveryService(
        AppDbContext context,
        IAgentAssignmentService agentAssignmentService,
        INotificationService? notificationService = null)
    {
        _context = context;
        _agentAssignmentService = agentAssignmentService;
        _notificationService = notificationService;
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

        if (previousAgentId.HasValue)
        {
            var prevAgent = await _context.Agents
                .Include(a => a.User)
                .Include(a => a.Zone)
                .FirstOrDefaultAsync(a => a.Id == previousAgentId.Value);

            if (prevAgent != null)
            {
                previousAgentDto = new AssignedAgentDto
                {
                    Id = prevAgent.Id,
                    Name = prevAgent.User?.FullName ?? "Agent",
                    Email = prevAgent.User?.Email ?? string.Empty,
                    ZoneName = prevAgent.Zone?.Name ?? "Unknown Zone",
                    DistanceKm = 0
                };
            }
        }

        var now = DateTime.UtcNow;
        AgentAssignmentResponse? newAssignmentResult = null;

        var executionStrategy = _context.Database.CreateExecutionStrategy();

        await executionStrategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // STEP 5: Release previous agent
                if (previousAgentId.HasValue)
                {
                    var prevAgent = await _context.Agents.FirstOrDefaultAsync(a => a.Id == previousAgentId.Value);
                    if (prevAgent != null)
                    {
                        prevAgent.IsAvailable = true;
                    }
                }

                // STEP 6: Update order status to Rescheduled & set RescheduledDate
                order.Status = OrderStatus.Rescheduled;
                order.RescheduledDate = request.RescheduledDate;
                order.AssignedAgentId = null;
                order.UpdatedAt = now;

                var historyEntry = new OrderStatusHistory
                {
                    OrderId = order.Id,
                    Status = OrderStatus.Rescheduled,
                    ActorId = customer.Id,
                    ActorRole = customer.Role,
                    Notes = $"Rescheduled for {request.RescheduledDate:yyyy-MM-dd}",
                    Timestamp = now
                };
                _context.OrderStatusHistories.Add(historyEntry);

                // Create customer in-app notification 1 (Reschedule confirmation)
                var notification1 = new Notification
                {
                    UserId = customer.Id,
                    OrderId = order.Id,
                    Title = $"Delivery Rescheduled - {order.TrackingNumber}",
                    RecipientEmail = customer.Email,
                    Message = $"Order {order.TrackingNumber} has been rescheduled for {request.RescheduledDate:yyyy-MM-dd}.",
                    IsRead = false,
                    Channel = CommunicationChannel.InApp,
                    EventType = "OrderRescheduled",
                    DeliveryStatus = CommunicationStatus.Sent,
                    SentAt = now
                };
                _context.Notifications.Add(notification1);

                await _context.SaveChangesAsync();

                // STEP 8-11: Trigger Auto-Assignment excluding previous agent
                newAssignmentResult = await _agentAssignmentService.AutoAssignAgentAsync(order.Id, excludeAgentId: previousAgentId);

                // Create customer in-app notification 2 (Reassignment confirmation)
                var notification2 = new Notification
                {
                    UserId = customer.Id,
                    OrderId = order.Id,
                    Title = $"Agent Reassigned - {order.TrackingNumber}",
                    RecipientEmail = customer.Email,
                    Message = $"Order {order.TrackingNumber} has been rescheduled and assigned to Agent {newAssignmentResult.AssignedAgent.Name}.",
                    IsRead = false,
                    Channel = CommunicationChannel.InApp,
                    EventType = "AgentReassigned",
                    DeliveryStatus = CommunicationStatus.Sent,
                    SentAt = DateTime.UtcNow
                };
                _context.Notifications.Add(notification2);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();

                // Fail-safe state restoration
                order.Status = previousStatus;
                order.AssignedAgentId = previousAgentId;

                if (previousAgentId.HasValue)
                {
                    var prevAgent = await _context.Agents.FirstOrDefaultAsync(a => a.Id == previousAgentId.Value);
                    if (prevAgent != null)
                    {
                        prevAgent.IsAvailable = false;
                    }
                }

                await _context.SaveChangesAsync();
                throw;
            }
        });

        // Trigger Multi-Channel Email/SMS notification via NotificationService (Failure safe)
        if (_notificationService != null)
        {
            try
            {
                await _notificationService.NotifyCustomerAsync(
                    customer.Id,
                    order.Id,
                    order.TrackingNumber,
                    "OrderRescheduled",
                    $"Delivery Rescheduled - {order.TrackingNumber}",
                    $"Your delivery for order {order.TrackingNumber} has been rescheduled to {request.RescheduledDate:yyyy-MM-dd} and reassigned to Agent {newAssignmentResult?.AssignedAgent.Name}.",
                    customer.Email);
            }
            catch
            {
                // Never fail recovery on notification issues
            }
        }

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
