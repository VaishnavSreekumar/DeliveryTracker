using Microsoft.EntityFrameworkCore;
using DeliveryTracker.API.Data;
using DeliveryTracker.API.DTOs;
using DeliveryTracker.API.Entities;
using DeliveryTracker.API.Enums;

namespace DeliveryTracker.API.Services;

public class OrderStatusService : IOrderStatusService
{
    private readonly AppDbContext _context;
    private readonly INotificationService? _notificationService;

    public OrderStatusService(AppDbContext context, INotificationService? notificationService = null)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<OrderStatusUpdateResponse> UpdateOrderStatusAsync(int orderId, UpdateOrderStatusRequest request)
    {
        // 1. Load Order
        var order = await _context.Orders
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
        {
            throw new KeyNotFoundException($"Order with ID {orderId} not found.");
        }

        // 2. Load User/Actor and load Role from Database (Client cannot supply ActorRole)
        var user = await _context.Users
            .Include(u => u.AgentProfile)
            .FirstOrDefaultAsync(u => u.Id == request.ActorId);

        if (user == null)
        {
            throw new KeyNotFoundException($"Actor/User with ID {request.ActorId} not found.");
        }

        // 3. Agent Ownership Rule: If Actor is Agent, verify assignment
        if (user.Role == UserRole.Agent)
        {
            var agent = user.AgentProfile 
                ?? await _context.Agents.FirstOrDefaultAsync(a => a.UserId == user.Id);

            if (agent == null || !order.AssignedAgentId.HasValue || order.AssignedAgentId.Value != agent.Id)
            {
                throw new UnauthorizedAccessException($"Agent '{user.FullName}' (ID {agent?.Id}) is not assigned to order '{order.TrackingNumber}'.");
            }
        }

        // 4. Validate State Machine Transition (Strict linear progression for standard updates)
        if (!IsValidTransition(order.Status, request.Status))
        {
            throw new InvalidOperationException($"Invalid status transition from '{order.Status}' to '{request.Status}'.");
        }

        var previousStatus = order.Status;
        var now = DateTime.UtcNow;

        // 5. Construct new immutable OrderStatusHistory entry
        var historyEntry = new OrderStatusHistory
        {
            OrderId = order.Id,
            Status = request.Status,
            ActorId = user.Id,
            ActorRole = user.Role, // Loaded strictly from DB User entity
            Notes = request.Notes ?? $"Status updated to {request.Status}",
            Timestamp = now
        };

        // 6. Update Order current state
        order.Status = request.Status;
        order.UpdatedAt = now;

        // 7. Atomic persistence using EF Execution Strategy
        var executionStrategy = _context.Database.CreateExecutionStrategy();

        await executionStrategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.OrderStatusHistories.Add(historyEntry);

                // If Status is Failed, record DeliveryAttempt & in-app notification & release agent
                if (request.Status == OrderStatus.Failed)
                {
                    int previousAttempts = await _context.DeliveryAttempts.CountAsync(d => d.OrderId == order.Id);
                    int agentId = order.AssignedAgentId ?? 0;

                    var deliveryAttempt = new DeliveryAttempt
                    {
                        OrderId = order.Id,
                        AgentId = agentId,
                        AttemptNumber = previousAttempts + 1,
                        FailureReason = request.Notes ?? "Delivery failed",
                        RescheduledDate = null,
                        AttemptedAt = now
                    };
                    _context.DeliveryAttempts.Add(deliveryAttempt);

                    var failureNotification = new Notification
                    {
                        UserId = order.CustomerId,
                        OrderId = order.Id,
                        Title = $"Delivery Attempt Failed - {order.TrackingNumber}",
                        RecipientEmail = order.Customer?.Email ?? "customer@delivery.com",
                        Message = $"Delivery attempt failed for order {order.TrackingNumber}. Reason: {request.Notes ?? "Delivery failed"}. Please reschedule your delivery.",
                        IsRead = false,
                        Channel = CommunicationChannel.InApp,
                        EventType = "DeliveryFailed",
                        DeliveryStatus = CommunicationStatus.Sent,
                        SentAt = now
                    };
                    _context.Notifications.Add(failureNotification);

                    if (order.AssignedAgentId.HasValue)
                    {
                        var agent = await _context.Agents.FindAsync(order.AssignedAgentId.Value);
                        if (agent != null) agent.IsAvailable = true;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });

        // 8. Trigger Multi-Channel Customer Notification (Failure-safe)
        if (_notificationService != null)
        {
            try
            {
                var (eventType, title, msg) = GetNotificationContentForTransition(order.TrackingNumber, request.Status, request.Notes);
                await _notificationService.NotifyCustomerAsync(
                    order.CustomerId,
                    order.Id,
                    order.TrackingNumber,
                    eventType,
                    title,
                    msg,
                    order.Customer?.Email);
            }
            catch
            {
                // Never fail status transition due to notification issues
            }
        }

        return new OrderStatusUpdateResponse
        {
            OrderId = order.Id,
            TrackingNumber = order.TrackingNumber,
            PreviousStatus = previousStatus,
            CurrentStatus = order.Status,
            UpdatedAt = order.UpdatedAt,
            HistoryEntry = new OrderStatusHistoryDto
            {
                Id = historyEntry.Id,
                Status = historyEntry.Status,
                ActorId = historyEntry.ActorId,
                ActorRole = historyEntry.ActorRole,
                Notes = historyEntry.Notes,
                Timestamp = historyEntry.Timestamp
            }
        };
    }

    public async Task<OrderStatusUpdateResponse> OverrideOrderStatusAsync(int orderId, AdminOverrideStatusRequest request, int adminUserId)
    {
        var order = await _context.Orders
            .Include(o => o.Customer)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
        {
            throw new KeyNotFoundException($"Order with ID {orderId} not found.");
        }

        var adminUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == adminUserId);
        if (adminUser == null || adminUser.Role != UserRole.Admin)
        {
            throw new UnauthorizedAccessException("Only administrators can perform status overrides.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new ArgumentException("A detailed override reason is mandatory for admin status overrides.");
        }

        var previousStatus = order.Status;
        var now = DateTime.UtcNow;

        var historyEntry = new OrderStatusHistory
        {
            OrderId = order.Id,
            Status = request.Status,
            ActorId = adminUser.Id,
            ActorRole = UserRole.Admin,
            Notes = $"ADMIN OVERRIDE: {request.Reason.Trim()}",
            Timestamp = now
        };

        order.Status = request.Status;
        order.UpdatedAt = now;

        var executionStrategy = _context.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.OrderStatusHistories.Add(historyEntry);

                // If overridden to Delivered or Failed, release assigned agent
                if (request.Status == OrderStatus.Delivered || request.Status == OrderStatus.Failed)
                {
                    if (order.AssignedAgentId.HasValue)
                    {
                        var agent = await _context.Agents.FindAsync(order.AssignedAgentId.Value);
                        if (agent != null)
                        {
                            agent.IsAvailable = true;
                        }
                    }
                }

                // If overridden to Failed, record attempt
                if (request.Status == OrderStatus.Failed)
                {
                    int previousAttempts = await _context.DeliveryAttempts.CountAsync(d => d.OrderId == order.Id);
                    int agentId = order.AssignedAgentId ?? 0;

                    var deliveryAttempt = new DeliveryAttempt
                    {
                        OrderId = order.Id,
                        AgentId = agentId,
                        AttemptNumber = previousAttempts + 1,
                        FailureReason = $"Admin Override: {request.Reason}",
                        RescheduledDate = null,
                        AttemptedAt = now
                    };
                    _context.DeliveryAttempts.Add(deliveryAttempt);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });

        // Trigger notification for Admin Override
        if (_notificationService != null)
        {
            try
            {
                await _notificationService.NotifyCustomerAsync(
                    order.CustomerId,
                    order.Id,
                    order.TrackingNumber,
                    "AdminStatusOverride",
                    $"Status Updated by Admin - {order.TrackingNumber}",
                    $"Order status was updated to {request.Status} by Administrator. Reason: {request.Reason}",
                    order.Customer?.Email);
            }
            catch
            {
                // Never fail override due to notification issues
            }
        }

        return new OrderStatusUpdateResponse
        {
            OrderId = order.Id,
            TrackingNumber = order.TrackingNumber,
            PreviousStatus = previousStatus,
            CurrentStatus = order.Status,
            UpdatedAt = order.UpdatedAt,
            HistoryEntry = new OrderStatusHistoryDto
            {
                Id = historyEntry.Id,
                Status = historyEntry.Status,
                ActorId = historyEntry.ActorId,
                ActorRole = historyEntry.ActorRole,
                Notes = historyEntry.Notes,
                Timestamp = historyEntry.Timestamp
            }
        };
    }

    private static (string eventType, string title, string message) GetNotificationContentForTransition(string trackingNumber, OrderStatus nextStatus, string? notes)
    {
        return nextStatus switch
        {
            OrderStatus.PickedUp => (
                "PickedUp",
                $"Order Picked Up - {trackingNumber}",
                $"Your package for order {trackingNumber} has been picked up and is in preparation."
            ),
            OrderStatus.InTransit => (
                "InTransit",
                $"Package In Transit - {trackingNumber}",
                $"Your package for order {trackingNumber} is currently in transit between delivery facilities."
            ),
            OrderStatus.OutForDelivery => (
                "OutForDelivery",
                $"Out For Delivery - {trackingNumber}",
                $"Your package for order {trackingNumber} is out for delivery today. Please ensure recipient availability."
            ),
            OrderStatus.Delivered => (
                "OrderDelivered",
                $"Package Delivered - {trackingNumber}",
                $"Your package for order {trackingNumber} has been successfully delivered. Thank you for using DeliveryTracker!"
            ),
            OrderStatus.Failed => (
                "DeliveryFailed",
                $"Delivery Attempt Failed - {trackingNumber}",
                $"Delivery attempt failed for order {trackingNumber}. Reason: {notes ?? "Delivery failed"}. Please reschedule your delivery."
            ),
            _ => (
                "StatusUpdate",
                $"Order Status Update - {trackingNumber}",
                $"Order {trackingNumber} status changed to {nextStatus}."
            )
        };
    }

    private static bool IsValidTransition(OrderStatus current, OrderStatus next)
    {
        return (current, next) switch
        {
            (OrderStatus.Created, OrderStatus.PickedUp) => true,
            (OrderStatus.PickedUp, OrderStatus.InTransit) => true,
            (OrderStatus.InTransit, OrderStatus.OutForDelivery) => true,
            (OrderStatus.OutForDelivery, OrderStatus.Delivered) => true,
            (OrderStatus.OutForDelivery, OrderStatus.Failed) => true,
            (OrderStatus.Rescheduled, OrderStatus.OutForDelivery) => true,
            _ => false
        };
    }
}
