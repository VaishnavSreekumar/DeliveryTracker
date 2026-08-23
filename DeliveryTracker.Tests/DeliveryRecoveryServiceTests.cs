using Microsoft.EntityFrameworkCore;
using DeliveryTracker.API.Data;
using DeliveryTracker.API.DTOs;
using DeliveryTracker.API.Entities;
using DeliveryTracker.API.Enums;
using DeliveryTracker.API.Services;
using Xunit;

namespace DeliveryTracker.Tests;

public class DeliveryRecoveryServiceTests
{
    private AppDbContext GetInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var context = new AppDbContext(options);

        // Seed Users
        var customer = new User { Id = 2, FullName = "John Customer", Email = "customer@delivery.com", PasswordHash = "dev", Role = UserRole.Customer };
        var customerB = new User { Id = 3, FullName = "Jane Customer", Email = "jane@delivery.com", PasswordHash = "dev", Role = UserRole.Customer };
        var agent1User = new User { Id = 101, FullName = "Raj Agent", Email = "agent1@delivery.com", PasswordHash = "dev", Role = UserRole.Agent };
        var agent2User = new User { Id = 102, FullName = "Vikram Agent", Email = "agent2@delivery.com", PasswordHash = "dev", Role = UserRole.Agent };
        context.Users.AddRange(customer, customerB, agent1User, agent2User);

        // Seed Zone & Area
        var zoneA = new Zone { Id = 1, Name = "Zone A", Code = "ZONE_A" };
        var zoneB = new Zone { Id = 2, Name = "Zone B", Code = "ZONE_B" };
        context.Zones.AddRange(zoneA, zoneB);

        var colaba = new Area { Id = 1, Name = "Colaba", Code = "COLABA", ZoneId = 1 };
        var andheri = new Area { Id = 3, Name = "Andheri", Code = "ANDHERI", ZoneId = 2 };
        context.Areas.AddRange(colaba, andheri);

        // Seed Agents
        // Agent 1: Zone A, Busy (assigned to order)
        var agent1 = new Agent { Id = 1, UserId = 101, ZoneId = 1, IsAvailable = false, Latitude = 18.9220, Longitude = 72.8347 };
        // Agent 2: Zone A, Available (candidate replacement)
        var agent2 = new Agent { Id = 2, UserId = 102, ZoneId = 1, IsAvailable = true, Latitude = 18.9220, Longitude = 72.8347 };
        context.Agents.AddRange(agent1, agent2);

        // Seed Rate Card
        var rateCard = new RateCard { Id = 1, OrderType = OrderType.B2C, IntraZoneRatePerKg = 40, InterZoneRatePerKg = 60, CODSurcharge = 40 };
        context.RateCards.Add(rateCard);

        context.SaveChanges();
        return context;
    }

    private Order CreateOutForDeliveryOrder(AppDbContext context, int assignedAgentId = 1)
    {
        var order = new Order
        {
            Id = 1,
            TrackingNumber = "LM-TEST-REC-001",
            CustomerId = 2,
            PickupAreaId = 1,
            DropAreaId = 3,
            Status = OrderStatus.OutForDelivery,
            AssignedAgentId = assignedAgentId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Orders.Add(order);

        context.OrderStatusHistories.Add(new OrderStatusHistory
        {
            Id = 1, OrderId = 1, Status = OrderStatus.Created, ActorId = 2, ActorRole = UserRole.Customer, Notes = "Created", Timestamp = DateTime.UtcNow
        });
        context.OrderStatusHistories.Add(new OrderStatusHistory
        {
            Id = 2, OrderId = 1, Status = OrderStatus.PickedUp, ActorId = 101, ActorRole = UserRole.Agent, Notes = "PickedUp", Timestamp = DateTime.UtcNow
        });
        context.OrderStatusHistories.Add(new OrderStatusHistory
        {
            Id = 3, OrderId = 1, Status = OrderStatus.InTransit, ActorId = 101, ActorRole = UserRole.Agent, Notes = "InTransit", Timestamp = DateTime.UtcNow
        });
        context.OrderStatusHistories.Add(new OrderStatusHistory
        {
            Id = 4, OrderId = 1, Status = OrderStatus.OutForDelivery, ActorId = 101, ActorRole = UserRole.Agent, Notes = "OutForDelivery", Timestamp = DateTime.UtcNow
        });

        context.SaveChanges();
        return order;
    }

    [Fact]
    public async Task Test1_FailedDeliveryCreatesAttempt()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test1_FailedDeliveryCreatesAttempt));
        CreateOutForDeliveryOrder(db, assignedAgentId: 1);
        var statusService = new OrderStatusService(db);

        // Act
        var response = await statusService.UpdateOrderStatusAsync(1, new UpdateOrderStatusRequest
        {
            Status = OrderStatus.Failed,
            ActorId = 101,
            Notes = "Customer address uncontactable"
        });

        // Assert
        Assert.Equal(OrderStatus.Failed, response.CurrentStatus);

        var attempt = await db.DeliveryAttempts.FirstOrDefaultAsync(d => d.OrderId == 1);
        Assert.NotNull(attempt);
        Assert.Equal(1, attempt.AgentId);
        Assert.Equal("Customer address uncontactable", attempt.FailureReason);
        Assert.Null(attempt.RescheduledDate);
    }

    [Fact]
    public async Task Test2_FailedDeliveryCreatesNotification()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test2_FailedDeliveryCreatesNotification));
        CreateOutForDeliveryOrder(db, assignedAgentId: 1);
        var statusService = new OrderStatusService(db);

        // Act
        await statusService.UpdateOrderStatusAsync(1, new UpdateOrderStatusRequest
        {
            Status = OrderStatus.Failed,
            ActorId = 101,
            Notes = "Gate locked"
        });

        // Assert
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.OrderId == 1);
        Assert.NotNull(notification);
        Assert.Equal("customer@delivery.com", notification.RecipientEmail);
        Assert.Contains("Delivery attempt failed", notification.Message);
        Assert.Contains("Gate locked", notification.Message);
    }

    [Fact]
    public async Task Test3_CustomerCanRescheduleFailedOrder()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test3_CustomerCanRescheduleFailedOrder));
        CreateOutForDeliveryOrder(db, assignedAgentId: 1);

        var statusService = new OrderStatusService(db);
        await statusService.UpdateOrderStatusAsync(1, new UpdateOrderStatusRequest { Status = OrderStatus.Failed, ActorId = 101, Notes = "Failed" });

        var assignmentService = new AgentAssignmentService(db);
        var recoveryService = new DeliveryRecoveryService(db, assignmentService);

        var futureDate = DateTime.UtcNow.AddDays(2);
        var request = new RescheduleOrderRequest
        {
            CustomerId = 2,
            RescheduledDate = futureDate,
            Notes = "Reschedule for Sunday morning"
        };

        // Act
        var response = await recoveryService.RescheduleOrderAsync(1, request);

        // Assert
        Assert.Equal(OrderStatus.Rescheduled, response.CurrentStatus);
        Assert.Equal(2, response.NewAgent.Id); // Reassigned to Agent 2
        Assert.Equal(1, response.PreviousAgent!.Id); // Old Agent 1 released

        var history = await db.OrderStatusHistories.Where(h => h.OrderId == 1).ToListAsync();
        Assert.Equal(6, history.Count); // Created, PickedUp, InTransit, OutForDelivery, Failed, Rescheduled
        Assert.Equal(OrderStatus.Rescheduled, history.Last().Status);
    }

    [Fact]
    public async Task Test4_WrongCustomerCannotReschedule_ThrowsException()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test4_WrongCustomerCannotReschedule_ThrowsException));
        CreateOutForDeliveryOrder(db);
        var statusService = new OrderStatusService(db);
        await statusService.UpdateOrderStatusAsync(1, new UpdateOrderStatusRequest { Status = OrderStatus.Failed, ActorId = 101, Notes = "Failed" });

        var assignmentService = new AgentAssignmentService(db);
        var recoveryService = new DeliveryRecoveryService(db, assignmentService);

        var request = new RescheduleOrderRequest
        {
            CustomerId = 3, // Wrong Customer ID (Jane)
            RescheduledDate = DateTime.UtcNow.AddDays(2)
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => recoveryService.RescheduleOrderAsync(1, request));
        Assert.Contains("does not own order", ex.Message);
    }

    [Fact]
    public async Task Test5_CannotRescheduleNonFailedOrder_ThrowsException()
    {
        // Arrange: Order is still OutForDelivery (not Failed)
        var db = GetInMemoryDbContext(nameof(Test5_CannotRescheduleNonFailedOrder_ThrowsException));
        CreateOutForDeliveryOrder(db);

        var assignmentService = new AgentAssignmentService(db);
        var recoveryService = new DeliveryRecoveryService(db, assignmentService);

        var request = new RescheduleOrderRequest
        {
            CustomerId = 2,
            RescheduledDate = DateTime.UtcNow.AddDays(2)
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => recoveryService.RescheduleOrderAsync(1, request));
        Assert.Contains("must be 'Failed'", ex.Message);
    }

    [Fact]
    public async Task Test6_OldAgentReleased()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test6_OldAgentReleased));
        CreateOutForDeliveryOrder(db, assignedAgentId: 1);

        var statusService = new OrderStatusService(db);
        await statusService.UpdateOrderStatusAsync(1, new UpdateOrderStatusRequest { Status = OrderStatus.Failed, ActorId = 101, Notes = "Failed" });

        var assignmentService = new AgentAssignmentService(db);
        var recoveryService = new DeliveryRecoveryService(db, assignmentService);

        // Act
        await recoveryService.RescheduleOrderAsync(1, new RescheduleOrderRequest
        {
            CustomerId = 2,
            RescheduledDate = DateTime.UtcNow.AddDays(2)
        });

        // Assert
        var oldAgent = await db.Agents.FindAsync(1);
        Assert.True(oldAgent!.IsAvailable); // Agent 1 released!
    }

    [Fact]
    public async Task Test7_NewAgentAssigned()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test7_NewAgentAssigned));
        CreateOutForDeliveryOrder(db, assignedAgentId: 1);

        var statusService = new OrderStatusService(db);
        await statusService.UpdateOrderStatusAsync(1, new UpdateOrderStatusRequest { Status = OrderStatus.Failed, ActorId = 101, Notes = "Failed" });

        var assignmentService = new AgentAssignmentService(db);
        var recoveryService = new DeliveryRecoveryService(db, assignmentService);

        // Act
        var response = await recoveryService.RescheduleOrderAsync(1, new RescheduleOrderRequest
        {
            CustomerId = 2,
            RescheduledDate = DateTime.UtcNow.AddDays(2)
        });

        // Assert
        Assert.NotEqual(1, response.NewAgent.Id);
        Assert.Equal(2, response.NewAgent.Id);

        var newAgent = await db.Agents.FindAsync(2);
        Assert.False(newAgent!.IsAvailable); // New agent 2 marked unavailable
    }

    [Fact]
    public async Task Test8_PreviousAgentExcludedFromReassignment()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test8_PreviousAgentExcludedFromReassignment));
        CreateOutForDeliveryOrder(db, assignedAgentId: 1);

        var statusService = new OrderStatusService(db);
        await statusService.UpdateOrderStatusAsync(1, new UpdateOrderStatusRequest { Status = OrderStatus.Failed, ActorId = 101, Notes = "Failed" });

        var assignmentService = new AgentAssignmentService(db);
        var recoveryService = new DeliveryRecoveryService(db, assignmentService);

        // Act
        var response = await recoveryService.RescheduleOrderAsync(1, new RescheduleOrderRequest
        {
            CustomerId = 2,
            RescheduledDate = DateTime.UtcNow.AddDays(2)
        });

        // Assert: Agent 1 was released, but was EXCLUDED from immediate reassignment, so Agent 2 is selected.
        Assert.Equal(2, response.NewAgent.Id);
    }

    [Fact]
    public async Task Test9_RescheduledToOutForDelivery_Success()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test9_RescheduledToOutForDelivery_Success));
        CreateOutForDeliveryOrder(db, assignedAgentId: 1);

        var statusService = new OrderStatusService(db);
        await statusService.UpdateOrderStatusAsync(1, new UpdateOrderStatusRequest { Status = OrderStatus.Failed, ActorId = 101, Notes = "Failed" });

        var assignmentService = new AgentAssignmentService(db);
        var recoveryService = new DeliveryRecoveryService(db, assignmentService);
        await recoveryService.RescheduleOrderAsync(1, new RescheduleOrderRequest { CustomerId = 2, RescheduledDate = DateTime.UtcNow.AddDays(2) });

        // Act: Update Rescheduled -> OutForDelivery using New Agent 2 (UserId 102)
        var response = await statusService.UpdateOrderStatusAsync(1, new UpdateOrderStatusRequest
        {
            Status = OrderStatus.OutForDelivery,
            ActorId = 102, // Agent 2's UserId
            Notes = "Out for delivery attempt 2"
        });

        // Assert
        Assert.Equal(OrderStatus.OutForDelivery, response.CurrentStatus);
    }

    [Fact]
    public async Task Test10_RescheduledToDeliveredDirectly_Rejects()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test10_RescheduledToDeliveredDirectly_Rejects));
        CreateOutForDeliveryOrder(db, assignedAgentId: 1);

        var statusService = new OrderStatusService(db);
        await statusService.UpdateOrderStatusAsync(1, new UpdateOrderStatusRequest { Status = OrderStatus.Failed, ActorId = 101, Notes = "Failed" });

        var assignmentService = new AgentAssignmentService(db);
        var recoveryService = new DeliveryRecoveryService(db, assignmentService);
        await recoveryService.RescheduleOrderAsync(1, new RescheduleOrderRequest { CustomerId = 2, RescheduledDate = DateTime.UtcNow.AddDays(2) });

        // Act & Assert: Attempt Rescheduled -> Delivered directly (Illegal skip)
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => statusService.UpdateOrderStatusAsync(1, new UpdateOrderStatusRequest
        {
            Status = OrderStatus.Delivered,
            ActorId = 102
        }));
        Assert.Contains("Invalid status transition", ex.Message);
    }

    [Fact]
    public async Task Test11_CompleteRecoveryLifecycle_8HistoryRecords()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test11_CompleteRecoveryLifecycle_8HistoryRecords));

        // Make Agent 1 available so Agent 1 is assigned initially
        var agent1Init = await db.Agents.FindAsync(1);
        agent1Init!.IsAvailable = true;
        await db.SaveChangesAsync();
        
        // 1. Created Order
        var order = new Order
        {
            Id = 1, TrackingNumber = "LM-FULL-001", CustomerId = 2, PickupAreaId = 1, DropAreaId = 1, Status = OrderStatus.Created, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Orders.Add(order);
        db.OrderStatusHistories.Add(new OrderStatusHistory { Id = 1, OrderId = 1, Status = OrderStatus.Created, ActorId = 2, ActorRole = UserRole.Customer, Notes = "Created", Timestamp = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var assignmentService = new AgentAssignmentService(db);
        var statusService = new OrderStatusService(db);
        var recoveryService = new DeliveryRecoveryService(db, assignmentService);

        // 2. Auto-Assign Agent 1
        await assignmentService.AutoAssignAgentAsync(1); // Agent 1 assigned

        // 3. Created -> PickedUp
        await statusService.UpdateOrderStatusAsync(1, new UpdateOrderStatusRequest { Status = OrderStatus.PickedUp, ActorId = 101, Notes = "Picked Up" });

        // 4. PickedUp -> InTransit
        await statusService.UpdateOrderStatusAsync(1, new UpdateOrderStatusRequest { Status = OrderStatus.InTransit, ActorId = 101, Notes = "In Transit" });

        // 5. InTransit -> OutForDelivery
        await statusService.UpdateOrderStatusAsync(1, new UpdateOrderStatusRequest { Status = OrderStatus.OutForDelivery, ActorId = 101, Notes = "Out For Delivery 1" });

        // 6. OutForDelivery -> Failed
        await statusService.UpdateOrderStatusAsync(1, new UpdateOrderStatusRequest { Status = OrderStatus.Failed, ActorId = 101, Notes = "Address Not Found" });

        // 7. Customer Reschedules (Reassigns to Agent 2)
        await recoveryService.RescheduleOrderAsync(1, new RescheduleOrderRequest { CustomerId = 2, RescheduledDate = DateTime.UtcNow.AddDays(2), Notes = "Rescheduled for Tuesday" });

        // 8. Rescheduled -> OutForDelivery (Agent 2)
        await statusService.UpdateOrderStatusAsync(1, new UpdateOrderStatusRequest { Status = OrderStatus.OutForDelivery, ActorId = 102, Notes = "Out For Delivery 2" });

        // 9. OutForDelivery -> Delivered (Agent 2)
        await statusService.UpdateOrderStatusAsync(1, new UpdateOrderStatusRequest { Status = OrderStatus.Delivered, ActorId = 102, Notes = "Delivered to Customer" });

        // Assert
        var finalOrder = await db.Orders.Include(o => o.StatusHistory).FirstOrDefaultAsync(o => o.Id == 1);
        Assert.NotNull(finalOrder);
        Assert.Equal(OrderStatus.Delivered, finalOrder.Status);
        Assert.Equal(2, finalOrder.AssignedAgentId);

        // Verify History Count == 8
        Assert.Equal(8, finalOrder.StatusHistory.Count);

        var historyList = finalOrder.StatusHistory.OrderBy(h => h.Id).ToList();
        Assert.Equal(OrderStatus.Created, historyList[0].Status);
        Assert.Equal(OrderStatus.PickedUp, historyList[1].Status);
        Assert.Equal(OrderStatus.InTransit, historyList[2].Status);
        Assert.Equal(OrderStatus.OutForDelivery, historyList[3].Status);
        Assert.Equal(OrderStatus.Failed, historyList[4].Status);
        Assert.Equal(OrderStatus.Rescheduled, historyList[5].Status);
        Assert.Equal(OrderStatus.OutForDelivery, historyList[6].Status);
        Assert.Equal(OrderStatus.Delivered, historyList[7].Status);

        // Verify DeliveryAttempts = 1
        Assert.Equal(1, await db.DeliveryAttempts.CountAsync(d => d.OrderId == 1));

        // Verify Notifications >= 3 (1 Failed + 2 Reschedule/Reassignment)
        Assert.True(await db.Notifications.CountAsync(n => n.OrderId == 1) >= 3);

        // Verify Agent Availabilities (Agent 1 released -> true, Agent 2 busy -> false)
        var agent1 = await db.Agents.FindAsync(1);
        var agent2 = await db.Agents.FindAsync(2);
        Assert.True(agent1!.IsAvailable);
        Assert.False(agent2!.IsAvailable);
    }

    [Fact]
    public async Task Test12_NoAvailableReplacementAgent_TransactionRollsBackCleanly()
    {
        // Arrange: Make Agent 2 busy as well
        var db = GetInMemoryDbContext(nameof(Test12_NoAvailableReplacementAgent_TransactionRollsBackCleanly));
        var agent2 = await db.Agents.FindAsync(2);
        agent2!.IsAvailable = false;
        await db.SaveChangesAsync();

        CreateOutForDeliveryOrder(db, assignedAgentId: 1);
        var statusService = new OrderStatusService(db);
        await statusService.UpdateOrderStatusAsync(1, new UpdateOrderStatusRequest { Status = OrderStatus.Failed, ActorId = 101, Notes = "Failed" });

        var assignmentService = new AgentAssignmentService(db);
        var recoveryService = new DeliveryRecoveryService(db, assignmentService);

        // Act & Assert: Attempt reschedule when no replacement agent exists
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => recoveryService.RescheduleOrderAsync(1, new RescheduleOrderRequest
        {
            CustomerId = 2,
            RescheduledDate = DateTime.UtcNow.AddDays(2)
        }));

        Assert.Contains("No available delivery agents found", ex.Message);

        // Verify Clean Rollback: Order remains Failed and assigned to Agent 1 (Agent 1 remains busy)
        var order = await db.Orders.FindAsync(1);
        Assert.Equal(OrderStatus.Failed, order!.Status);
        Assert.Equal(1, order.AssignedAgentId);

        var agent1 = await db.Agents.FindAsync(1);
        Assert.False(agent1!.IsAvailable); // Retains original state
    }
}
