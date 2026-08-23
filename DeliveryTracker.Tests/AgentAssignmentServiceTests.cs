using Microsoft.EntityFrameworkCore;
using DeliveryTracker.API.Data;
using DeliveryTracker.API.DTOs;
using DeliveryTracker.API.Entities;
using DeliveryTracker.API.Enums;
using DeliveryTracker.API.Services;
using Xunit;

namespace DeliveryTracker.Tests;

public class AgentAssignmentServiceTests
{
    private AppDbContext GetInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var context = new AppDbContext(options);

        // Seed Zones
        var zoneA = new Zone { Id = 1, Name = "Zone A", Code = "ZONE_A" };
        var zoneB = new Zone { Id = 2, Name = "Zone B", Code = "ZONE_B" };
        var zoneC = new Zone { Id = 3, Name = "Zone C", Code = "ZONE_C" };
        context.Zones.AddRange(zoneA, zoneB, zoneC);

        // Seed Areas
        var colaba = new Area { Id = 1, Name = "Colaba", Code = "COLABA", ZoneId = 1 };
        var andheri = new Area { Id = 3, Name = "Andheri", Code = "ANDHERI", ZoneId = 2 };
        var thane = new Area { Id = 5, Name = "Thane", Code = "THANE", ZoneId = 3 };
        context.Areas.AddRange(colaba, andheri, thane);

        // Seed Users for Agents
        var user1 = new User { Id = 101, FullName = "Agent One", Email = "agent1@delivery.com", PasswordHash = "dev", Role = UserRole.Agent };
        var user2 = new User { Id = 102, FullName = "Agent Two", Email = "agent2@delivery.com", PasswordHash = "dev", Role = UserRole.Agent };
        var customer = new User { Id = 2, FullName = "John Customer", Email = "customer@delivery.com", PasswordHash = "dev", Role = UserRole.Customer };
        context.Users.AddRange(user1, user2, customer);

        // Seed Agents
        // Agent 1: Zone A (Colaba)
        var agent1 = new Agent
        {
            Id = 1,
            UserId = 101,
            ZoneId = 1,
            IsAvailable = true,
            Latitude = 18.9220,
            Longitude = 72.8347
        };

        // Agent 2: Zone B (Andheri)
        var agent2 = new Agent
        {
            Id = 2,
            UserId = 102,
            ZoneId = 2,
            IsAvailable = true,
            Latitude = 19.1197,
            Longitude = 72.8464
        };

        context.Agents.AddRange(agent1, agent2);

        // Seed Rate Cards
        var b2cRateCard = new RateCard { Id = 1, OrderType = OrderType.B2C, IntraZoneRatePerKg = 40.00m, InterZoneRatePerKg = 60.00m, CODSurcharge = 40.00m };
        context.RateCards.Add(b2cRateCard);

        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task Test1_AvailableAgentAssigned()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test1_AvailableAgentAssigned));
        var assignmentService = new AgentAssignmentService(db);

        // Create order in Zone A (Pickup = Colaba)
        var order = new Order
        {
            Id = 1,
            TrackingNumber = "LM-TEST-001",
            CustomerId = 2,
            PickupAreaId = 1, // Colaba (Zone A)
            DropAreaId = 3,   // Andheri (Zone B)
            Status = OrderStatus.Created,
            AssignedAgentId = null
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        // Act
        var result = await assignmentService.AutoAssignAgentAsync(1);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.AssignedAgent.Id); // Agent 1 is in Zone A
        Assert.Equal("Agent One", result.AssignedAgent.Name);

        // Verify Persistence
        var reloadedOrder = await db.Orders.FindAsync(1);
        Assert.Equal(1, reloadedOrder!.AssignedAgentId);

        var reloadedAgent1 = await db.Agents.FindAsync(1);
        Assert.False(reloadedAgent1!.IsAvailable); // Marked unavailable
    }

    [Fact]
    public async Task Test2_SameZonePreference()
    {
        // Arrange: Order pickup in Zone B (Andheri)
        // Agent 1 is in Zone A, Agent 2 is in Zone B. Both available.
        var db = GetInMemoryDbContext(nameof(Test2_SameZonePreference));
        var assignmentService = new AgentAssignmentService(db);

        var order = new Order
        {
            Id = 2,
            TrackingNumber = "LM-TEST-002",
            CustomerId = 2,
            PickupAreaId = 3, // Andheri (Zone B)
            DropAreaId = 1,   // Colaba (Zone A)
            Status = OrderStatus.Created,
            AssignedAgentId = null
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        // Act
        var result = await assignmentService.AutoAssignAgentAsync(2);

        // Assert
        Assert.Equal(2, result.AssignedAgent.Id); // Agent 2 selected because of same zone preference (Zone B)
        Assert.Equal("Agent Two", result.AssignedAgent.Name);
    }

    [Fact]
    public async Task Test3_BusyAgentExcluded()
    {
        // Arrange: Order pickup in Zone B (Andheri).
        // Agent 2 (Zone B) is busy (IsAvailable = false). Agent 1 (Zone A) is available.
        var db = GetInMemoryDbContext(nameof(Test3_BusyAgentExcluded));

        var agent2 = await db.Agents.FindAsync(2);
        agent2!.IsAvailable = false;
        await db.SaveChangesAsync();

        var assignmentService = new AgentAssignmentService(db);

        var order = new Order
        {
            Id = 3,
            TrackingNumber = "LM-TEST-003",
            CustomerId = 2,
            PickupAreaId = 3, // Andheri (Zone B)
            DropAreaId = 1,   // Colaba (Zone A)
            Status = OrderStatus.Created,
            AssignedAgentId = null
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        // Act
        var result = await assignmentService.AutoAssignAgentAsync(3);

        // Assert
        Assert.Equal(1, result.AssignedAgent.Id); // Agent 1 selected because Agent 2 was busy
        Assert.Equal("Agent One", result.AssignedAgent.Name);
    }

    [Fact]
    public async Task Test4_NoAgentsAvailable_ThrowsException()
    {
        // Arrange: Set all agents busy
        var db = GetInMemoryDbContext(nameof(Test4_NoAgentsAvailable_ThrowsException));
        var agents = await db.Agents.ToListAsync();
        foreach (var a in agents) a.IsAvailable = false;
        await db.SaveChangesAsync();

        var assignmentService = new AgentAssignmentService(db);

        var order = new Order
        {
            Id = 4,
            TrackingNumber = "LM-TEST-004",
            CustomerId = 2,
            PickupAreaId = 1,
            DropAreaId = 3,
            Status = OrderStatus.Created,
            AssignedAgentId = null
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => assignmentService.AutoAssignAgentAsync(4));
        Assert.Contains("No available delivery agents found", ex.Message);

        var reloadedOrder = await db.Orders.FindAsync(4);
        Assert.Null(reloadedOrder!.AssignedAgentId); // Order remains unassigned
    }

    [Fact]
    public async Task Test5_AlreadyAssignedOrder_ThrowsException()
    {
        // Arrange: Order already has Agent 1 assigned
        var db = GetInMemoryDbContext(nameof(Test5_AlreadyAssignedOrder_ThrowsException));
        var assignmentService = new AgentAssignmentService(db);

        var order = new Order
        {
            Id = 5,
            TrackingNumber = "LM-TEST-005",
            CustomerId = 2,
            PickupAreaId = 1,
            DropAreaId = 3,
            Status = OrderStatus.Created,
            AssignedAgentId = 1 // Already assigned
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => assignmentService.AutoAssignAgentAsync(5));
        Assert.Contains("already assigned", ex.Message);
    }

    [Fact]
    public async Task Test6_AssignmentPersistence()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test6_AssignmentPersistence));
        var assignmentService = new AgentAssignmentService(db);

        var order = new Order
        {
            Id = 6,
            TrackingNumber = "LM-TEST-006",
            CustomerId = 2,
            PickupAreaId = 1, // Colaba (Zone A)
            DropAreaId = 3,
            Status = OrderStatus.Created,
            AssignedAgentId = null
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        // Act
        await assignmentService.AutoAssignAgentAsync(6);

        // Assert: Reload from DB
        var dbOrder = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == 6);
        var dbAgent = await db.Agents.AsNoTracking().FirstOrDefaultAsync(a => a.Id == 1);

        Assert.NotNull(dbOrder);
        Assert.Equal(1, dbOrder.AssignedAgentId);

        Assert.NotNull(dbAgent);
        Assert.False(dbAgent.IsAvailable);
    }
}
