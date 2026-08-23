using Microsoft.EntityFrameworkCore;
using DeliveryTracker.API.Data;
using DeliveryTracker.API.DTOs;
using DeliveryTracker.API.Entities;
using DeliveryTracker.API.Enums;
using DeliveryTracker.API.Services;
using Xunit;

namespace DeliveryTracker.Tests;

public class OrderStatusServiceTests
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
        var agent1User = new User { Id = 101, FullName = "Raj Agent", Email = "agent1@delivery.com", PasswordHash = "dev", Role = UserRole.Agent };
        var agent2User = new User { Id = 102, FullName = "Vikram Agent", Email = "agent2@delivery.com", PasswordHash = "dev", Role = UserRole.Agent };
        var adminUser = new User { Id = 1, FullName = "System Admin", Email = "admin@delivery.com", PasswordHash = "dev", Role = UserRole.Admin };
        context.Users.AddRange(customer, agent1User, agent2User, adminUser);

        // Seed Zone & Area
        var zoneA = new Zone { Id = 1, Name = "Zone A", Code = "ZONE_A" };
        context.Zones.Add(zoneA);

        var colaba = new Area { Id = 1, Name = "Colaba", Code = "COLABA", ZoneId = 1 };
        context.Areas.Add(colaba);

        // Seed Agents (Agent 1 = Id 1, Agent 2 = Id 2)
        var agent1 = new Agent { Id = 1, UserId = 101, ZoneId = 1, IsAvailable = false, Latitude = 18.9220, Longitude = 72.8347 };
        var agent2 = new Agent { Id = 2, UserId = 102, ZoneId = 1, IsAvailable = false, Latitude = 18.9220, Longitude = 72.8347 };
        context.Agents.AddRange(agent1, agent2);

        // Seed Rate Card
        var rateCard = new RateCard { Id = 1, OrderType = OrderType.B2C, IntraZoneRatePerKg = 40, InterZoneRatePerKg = 60, CODSurcharge = 40 };
        context.RateCards.Add(rateCard);

        context.SaveChanges();
        return context;
    }

    private Order CreateBaseOrder(AppDbContext context, OrderStatus initialStatus = OrderStatus.Created, int? assignedAgentId = 1)
    {
        var order = new Order
        {
            Id = 1,
            TrackingNumber = "LM-TEST-STATUS-001",
            CustomerId = 2,
            PickupAreaId = 1,
            DropAreaId = 1,
            Status = initialStatus,
            AssignedAgentId = assignedAgentId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.Orders.Add(order);

        // Add initial Created history entry
        context.OrderStatusHistories.Add(new OrderStatusHistory
        {
            Id = 1,
            OrderId = 1,
            Status = OrderStatus.Created,
            ActorId = 2,
            ActorRole = UserRole.Customer,
            Notes = "Order created",
            Timestamp = DateTime.UtcNow
        });

        context.SaveChanges();
        return order;
    }

    [Fact]
    public async Task Test1_CreatedToPickedUp_Success()
    {
        var db = GetInMemoryDbContext(nameof(Test1_CreatedToPickedUp_Success));
        CreateBaseOrder(db, OrderStatus.Created, assignedAgentId: 1);

        var statusService = new OrderStatusService(db);

        var request = new UpdateOrderStatusRequest
        {
            Status = OrderStatus.PickedUp,
            ActorId = 101, // Agent 1's UserId
            Notes = "Package collected from customer"
        };

        var response = await statusService.UpdateOrderStatusAsync(1, request);

        Assert.Equal(OrderStatus.Created, response.PreviousStatus);
        Assert.Equal(OrderStatus.PickedUp, response.CurrentStatus);
        Assert.Equal(OrderStatus.PickedUp, response.HistoryEntry.Status);
        Assert.Equal(UserRole.Agent, response.HistoryEntry.ActorRole);
        Assert.Equal(2, await db.OrderStatusHistories.CountAsync());
    }

    [Fact]
    public async Task Test2_PickedUpToInTransit_Success()
    {
        var db = GetInMemoryDbContext(nameof(Test2_PickedUpToInTransit_Success));
        CreateBaseOrder(db, OrderStatus.PickedUp, assignedAgentId: 1);

        var statusService = new OrderStatusService(db);

        var response = await statusService.UpdateOrderStatusAsync(1, new UpdateOrderStatusRequest
        {
            Status = OrderStatus.InTransit,
            ActorId = 101,
            Notes = "In transit to hub"
        });

        Assert.Equal(OrderStatus.InTransit, response.CurrentStatus);
    }

    [Fact]
    public async Task Test3_InTransitToOutForDelivery_Success()
    {
        var db = GetInMemoryDbContext(nameof(Test3_InTransitToOutForDelivery_Success));
        CreateBaseOrder(db, OrderStatus.InTransit, assignedAgentId: 1);

        var statusService = new OrderStatusService(db);

        var response = await statusService.UpdateOrderStatusAsync(1, new UpdateOrderStatusRequest
        {
            Status = OrderStatus.OutForDelivery,
            ActorId = 101,
            Notes = "Out for delivery"
        });

        Assert.Equal(OrderStatus.OutForDelivery, response.CurrentStatus);
    }

    [Fact]
    public async Task Test4_OutForDeliveryToDelivered_Success()
    {
        var db = GetInMemoryDbContext(nameof(Test4_OutForDeliveryToDelivered_Success));
        CreateBaseOrder(db, OrderStatus.OutForDelivery, assignedAgentId: 1);

        var statusService = new OrderStatusService(db);

        var response = await statusService.UpdateOrderStatusAsync(1, new UpdateOrderStatusRequest
        {
            Status = OrderStatus.Delivered,
            ActorId = 101,
            Notes = "Package delivered to recipient"
        });

        Assert.Equal(OrderStatus.Delivered, response.CurrentStatus);
    }

    [Fact]
    public async Task Test5_OutForDeliveryToFailed_Success()
    {
        var db = GetInMemoryDbContext(nameof(Test5_OutForDeliveryToFailed_Success));
        CreateBaseOrder(db, OrderStatus.OutForDelivery, assignedAgentId: 1);

        var statusService = new OrderStatusService(db);

        var response = await statusService.UpdateOrderStatusAsync(1, new UpdateOrderStatusRequest
        {
            Status = OrderStatus.Failed,
            ActorId = 101,
            Notes = "Customer unavailable"
        });

        Assert.Equal(OrderStatus.Failed, response.CurrentStatus);
    }

    [Fact]
    public async Task Test6_InvalidTransition_CreatedToDelivered_Rejects()
    {
        var db = GetInMemoryDbContext(nameof(Test6_InvalidTransition_CreatedToDelivered_Rejects));
        CreateBaseOrder(db, OrderStatus.Created, assignedAgentId: 1);

        var statusService = new OrderStatusService(db);

        var request = new UpdateOrderStatusRequest
        {
            Status = OrderStatus.Delivered,
            ActorId = 101,
            Notes = "Attempt skip to delivered"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => statusService.UpdateOrderStatusAsync(1, request));
        Assert.Contains("Invalid status transition", ex.Message);

        var order = await db.Orders.FindAsync(1);
        Assert.Equal(OrderStatus.Created, order!.Status);
        Assert.Equal(1, await db.OrderStatusHistories.CountAsync()); // Unchanged history count
    }

    [Fact]
    public async Task Test7_InvalidTransition_CreatedToInTransit_Rejects()
    {
        var db = GetInMemoryDbContext(nameof(Test7_InvalidTransition_CreatedToInTransit_Rejects));
        CreateBaseOrder(db, OrderStatus.Created, assignedAgentId: 1);

        var statusService = new OrderStatusService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => statusService.UpdateOrderStatusAsync(1, new UpdateOrderStatusRequest
        {
            Status = OrderStatus.InTransit,
            ActorId = 101
        }));
    }

    [Fact]
    public async Task Test8_DeliveredIsTerminal_RejectsTransition()
    {
        var db = GetInMemoryDbContext(nameof(Test8_DeliveredIsTerminal_RejectsTransition));
        CreateBaseOrder(db, OrderStatus.Delivered, assignedAgentId: 1);

        var statusService = new OrderStatusService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => statusService.UpdateOrderStatusAsync(1, new UpdateOrderStatusRequest
        {
            Status = OrderStatus.InTransit,
            ActorId = 101
        }));
        Assert.Contains("Invalid status transition", ex.Message);
    }

    [Fact]
    public async Task Test9_FailedIsTerminalForPhase5_RejectsTransition()
    {
        var db = GetInMemoryDbContext(nameof(Test9_FailedIsTerminalForPhase5_RejectsTransition));
        CreateBaseOrder(db, OrderStatus.Failed, assignedAgentId: 1);

        var statusService = new OrderStatusService(db);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => statusService.UpdateOrderStatusAsync(1, new UpdateOrderStatusRequest
        {
            Status = OrderStatus.Delivered,
            ActorId = 101
        }));
        Assert.Contains("Invalid status transition", ex.Message);
    }

    [Fact]
    public async Task Test10_AgentOwnership_UnassignedAgentAttemptFails()
    {
        var db = GetInMemoryDbContext(nameof(Test10_AgentOwnership_UnassignedAgentAttemptFails));
        CreateBaseOrder(db, OrderStatus.Created, assignedAgentId: 1); // Assigned to Agent 1 (UserId 101)

        var statusService = new OrderStatusService(db);

        // Agent 2 (UserId 102) attempts to update Agent 1's order
        var request = new UpdateOrderStatusRequest
        {
            Status = OrderStatus.PickedUp,
            ActorId = 102, // Agent 2
            Notes = "Unauthorized update attempt"
        };

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => statusService.UpdateOrderStatusAsync(1, request));
        Assert.Contains("is not assigned to order", ex.Message);

        var order = await db.Orders.FindAsync(1);
        Assert.Equal(OrderStatus.Created, order!.Status); // Unchanged
    }

    [Fact]
    public async Task Test11_ActorRoleLoadedFromDatabase()
    {
        var db = GetInMemoryDbContext(nameof(Test11_ActorRoleLoadedFromDatabase));
        CreateBaseOrder(db, OrderStatus.Created, assignedAgentId: 1);

        var statusService = new OrderStatusService(db);

        var response = await statusService.UpdateOrderStatusAsync(1, new UpdateOrderStatusRequest
        {
            Status = OrderStatus.PickedUp,
            ActorId = 101, // DB Role is Agent
            Notes = "Role loaded from DB"
        });

        Assert.Equal(UserRole.Agent, response.HistoryEntry.ActorRole);
    }

    [Fact]
    public async Task Test12_TrackingHistoryImmutability_FullLifecycle()
    {
        var db = GetInMemoryDbContext(nameof(Test12_TrackingHistoryImmutability_FullLifecycle));
        CreateBaseOrder(db, OrderStatus.Created, assignedAgentId: 1);

        var statusService = new OrderStatusService(db);

        // 1. Created -> PickedUp
        await statusService.UpdateOrderStatusAsync(1, new UpdateOrderStatusRequest { Status = OrderStatus.PickedUp, ActorId = 101, Notes = "Picked up" });

        // 2. PickedUp -> InTransit
        await statusService.UpdateOrderStatusAsync(1, new UpdateOrderStatusRequest { Status = OrderStatus.InTransit, ActorId = 101, Notes = "In transit" });

        // 3. InTransit -> OutForDelivery
        await statusService.UpdateOrderStatusAsync(1, new UpdateOrderStatusRequest { Status = OrderStatus.OutForDelivery, ActorId = 101, Notes = "Out for delivery" });

        // 4. OutForDelivery -> Delivered
        await statusService.UpdateOrderStatusAsync(1, new UpdateOrderStatusRequest { Status = OrderStatus.Delivered, ActorId = 101, Notes = "Delivered" });

        // Assert
        var order = await db.Orders.Include(o => o.StatusHistory).FirstOrDefaultAsync(o => o.Id == 1);
        Assert.NotNull(order);
        Assert.Equal(OrderStatus.Delivered, order.Status);

        // Verify exactly 5 immutable history records
        Assert.Equal(5, order.StatusHistory.Count);

        var historyList = order.StatusHistory.OrderBy(h => h.Id).ToList();

        Assert.Equal(OrderStatus.Created, historyList[0].Status);
        Assert.Equal("Order created", historyList[0].Notes);
        Assert.Equal(UserRole.Customer, historyList[0].ActorRole);

        Assert.Equal(OrderStatus.PickedUp, historyList[1].Status);
        Assert.Equal(OrderStatus.InTransit, historyList[2].Status);
        Assert.Equal(OrderStatus.OutForDelivery, historyList[3].Status);
        Assert.Equal(OrderStatus.Delivered, historyList[4].Status);
    }
}
