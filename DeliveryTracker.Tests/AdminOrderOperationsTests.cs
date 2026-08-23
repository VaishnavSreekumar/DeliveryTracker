using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DeliveryTracker.API.Controllers;
using DeliveryTracker.API.Data;
using DeliveryTracker.API.DTOs;
using DeliveryTracker.API.Entities;
using DeliveryTracker.API.Enums;
using DeliveryTracker.API.Services;
using Xunit;

namespace DeliveryTracker.Tests;

public class AdminOrderOperationsTests
{
    private AppDbContext GetInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var context = new AppDbContext(options);

        // Seed Users
        var adminUser = new User { Id = 1, FullName = "System Admin", Email = "admin@delivery.com", PasswordHash = "dev", Role = UserRole.Admin };
        var custA = new User { Id = 2, FullName = "Customer Alice", Email = "alice@delivery.com", PasswordHash = "dev", Role = UserRole.Customer };
        var custB = new User { Id = 3, FullName = "Customer Bob", Email = "bob@delivery.com", PasswordHash = "dev", Role = UserRole.Customer };
        var agentUser1 = new User { Id = 101, FullName = "Agent One", Email = "agent1@delivery.com", PasswordHash = "dev", Role = UserRole.Agent };
        var agentUser2 = new User { Id = 102, FullName = "Agent Two", Email = "agent2@delivery.com", PasswordHash = "dev", Role = UserRole.Agent };
        context.Users.AddRange(adminUser, custA, custB, agentUser1, agentUser2);

        // Seed Zones & Areas
        var zone1 = new Zone { Id = 1, Name = "South Mumbai", Code = "ZONE_A" };
        var zone2 = new Zone { Id = 2, Name = "Western Suburbs", Code = "ZONE_B" };
        context.Zones.AddRange(zone1, zone2);

        var area1 = new Area { Id = 1, Name = "Colaba", Code = "COLABA", ZoneId = 1 };
        var area2 = new Area { Id = 2, Name = "Andheri", Code = "ANDHERI", ZoneId = 2 };
        context.Areas.AddRange(area1, area2);

        // Seed Agents
        var agent1 = new Agent { Id = 1, UserId = 101, ZoneId = 1, IsAvailable = true, Latitude = 18.9220, Longitude = 72.8347 };
        var agent2 = new Agent { Id = 2, UserId = 102, ZoneId = 2, IsAvailable = true, Latitude = 19.1197, Longitude = 72.8464 };
        context.Agents.AddRange(agent1, agent2);

        // Seed Rate Cards
        var b2cRateCard = new RateCard { Id = 1, OrderType = OrderType.B2C, IntraZoneRatePerKg = 40.00m, InterZoneRatePerKg = 60.00m, CODSurcharge = 40.00m };
        var b2bRateCard = new RateCard { Id = 2, OrderType = OrderType.B2B, IntraZoneRatePerKg = 30.00m, InterZoneRatePerKg = 50.00m, CODSurcharge = 30.00m };
        context.RateCards.AddRange(b2cRateCard, b2bRateCard);

        context.SaveChanges();
        return context;
    }

    private void SetControllerUser(ControllerBase controller, int userId, UserRole role, string email)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("sub", userId.ToString()),
            new(ClaimTypes.Role, role.ToString()),
            new(ClaimTypes.Email, email)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    [Fact]
    public async Task Test1_Admin_CanCreateOrder_OnBehalfOfCustomer()
    {
        var db = GetInMemoryDbContext(nameof(Test1_Admin_CanCreateOrder_OnBehalfOfCustomer));
        var pricingService = new PricingService(db);
        var orderService = new OrderService(db, pricingService);
        var assignmentService = new AgentAssignmentService(db);
        var statusService = new OrderStatusService(db);
        var recoveryService = new DeliveryRecoveryService(db, assignmentService);

        var controller = new OrdersController(orderService, assignmentService, statusService, recoveryService, db);
        SetControllerUser(controller, 1, UserRole.Admin, "admin@delivery.com");

        var result = await controller.CreateOrder(new CreateOrderRequest
        {
            CustomerId = 2, // Customer Alice
            PickupAreaId = 1,
            DropAreaId = 2,
            PickupAddress = "101 Colaba Causeway",
            DropAddress = "202 Link Road",
            Length = 20,
            Breadth = 20,
            Height = 20,
            ActualWeight = 4.0m,
            OrderType = OrderType.B2C,
            PaymentType = PaymentType.Prepaid
        });

        var createdAtResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var order = Assert.IsType<OrderResponse>(createdAtResult.Value);

        Assert.Equal(2, order.CustomerId);
        Assert.Equal("Customer Alice", order.CustomerName);
        Assert.Equal(OrderStatus.Created, order.Status);

        // Verify initial audit trail shows Admin creator
        var initialHistory = order.StatusHistory.First();
        Assert.Equal(1, initialHistory.ActorId);
        Assert.Equal(UserRole.Admin, initialHistory.ActorRole);
        Assert.Contains("Admin", initialHistory.Notes);
    }

    [Fact]
    public async Task Test2_Admin_CanManuallyAssignAgent()
    {
        var db = GetInMemoryDbContext(nameof(Test2_Admin_CanManuallyAssignAgent));
        var pricingService = new PricingService(db);
        var orderService = new OrderService(db, pricingService);
        var assignmentService = new AgentAssignmentService(db);
        var statusService = new OrderStatusService(db);
        var recoveryService = new DeliveryRecoveryService(db, assignmentService);

        // Create order
        var order = await orderService.CreateOrderAsync(new CreateOrderRequest
        {
            CustomerId = 2,
            PickupAreaId = 1,
            DropAreaId = 1,
            PickupAddress = "Addr 1",
            DropAddress = "Addr 2",
            Length = 10, Breadth = 10, Height = 10, ActualWeight = 2.0m,
            OrderType = OrderType.B2C, PaymentType = PaymentType.Prepaid
        });

        var controller = new OrdersController(orderService, assignmentService, statusService, recoveryService, db);
        SetControllerUser(controller, 1, UserRole.Admin, "admin@delivery.com");

        // Manually assign Agent 2
        var result = await controller.ManualAssignAgent(order.Id, new ManualAssignAgentRequest { AgentId = 2 });
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var assignResponse = Assert.IsType<AgentAssignmentResponse>(okResult.Value);

        Assert.Equal(2, assignResponse.AssignedAgent.Id);

        // Verify Agent 2 availability is updated to false
        var agent2 = await db.Agents.FindAsync(2);
        Assert.False(agent2!.IsAvailable);

        // Verify Order in DB has AssignedAgentId = 2
        var dbOrder = await db.Orders.FindAsync(order.Id);
        Assert.Equal(2, dbOrder!.AssignedAgentId);
    }

    [Fact]
    public async Task Test3_Admin_CanOverrideOrderStatus_WithMandatoryReason()
    {
        var db = GetInMemoryDbContext(nameof(Test3_Admin_CanOverrideOrderStatus_WithMandatoryReason));
        var pricingService = new PricingService(db);
        var orderService = new OrderService(db, pricingService);
        var assignmentService = new AgentAssignmentService(db);
        var statusService = new OrderStatusService(db);
        var recoveryService = new DeliveryRecoveryService(db, assignmentService);

        var order = await orderService.CreateOrderAsync(new CreateOrderRequest
        {
            CustomerId = 2,
            PickupAreaId = 1,
            DropAreaId = 1,
            PickupAddress = "Addr 1",
            DropAddress = "Addr 2",
            Length = 10, Breadth = 10, Height = 10, ActualWeight = 2.0m,
            OrderType = OrderType.B2C, PaymentType = PaymentType.Prepaid
        });

        var controller = new OrdersController(orderService, assignmentService, statusService, recoveryService, db);
        SetControllerUser(controller, 1, UserRole.Admin, "admin@delivery.com");

        // Override directly from Created to Delivered
        var overrideResult = await controller.OverrideOrderStatus(order.Id, new AdminOverrideStatusRequest
        {
            Status = OrderStatus.Delivered,
            Reason = "Customer picked up directly from distribution hub under express authorization."
        });

        var okResult = Assert.IsType<OkObjectResult>(overrideResult.Result);
        var updateResponse = Assert.IsType<OrderStatusUpdateResponse>(okResult.Value);

        Assert.Equal(OrderStatus.Delivered, updateResponse.CurrentStatus);
        Assert.Equal(1, updateResponse.HistoryEntry.ActorId);
        Assert.Equal(UserRole.Admin, updateResponse.HistoryEntry.ActorRole);
        Assert.Contains("ADMIN OVERRIDE", updateResponse.HistoryEntry.Notes);

        var dbOrder = await db.Orders.FindAsync(order.Id);
        Assert.Equal(OrderStatus.Delivered, dbOrder!.Status);
    }

    [Fact]
    public async Task Test4_Admin_OrderFiltering_ByStatusZoneAndAgent()
    {
        var db = GetInMemoryDbContext(nameof(Test4_Admin_OrderFiltering_ByStatusZoneAndAgent));
        var pricingService = new PricingService(db);
        var orderService = new OrderService(db, pricingService);
        var assignmentService = new AgentAssignmentService(db);
        var statusService = new OrderStatusService(db);
        var recoveryService = new DeliveryRecoveryService(db, assignmentService);

        // Create Order 1 in Zone 1 (South Mumbai)
        var o1 = await orderService.CreateOrderAsync(new CreateOrderRequest
        {
            CustomerId = 2,
            PickupAreaId = 1, // Zone 1
            DropAreaId = 1,   // Zone 1
            PickupAddress = "A", DropAddress = "B",
            Length = 10, Breadth = 10, Height = 10, ActualWeight = 1.0m,
            OrderType = OrderType.B2C, PaymentType = PaymentType.Prepaid
        });

        // Create Order 2 in Zone 2 (Western Suburbs)
        var o2 = await orderService.CreateOrderAsync(new CreateOrderRequest
        {
            CustomerId = 3,
            PickupAreaId = 2, // Zone 2
            DropAreaId = 2,   // Zone 2
            PickupAddress = "C", DropAddress = "D",
            Length = 10, Breadth = 10, Height = 10, ActualWeight = 1.0m,
            OrderType = OrderType.B2C, PaymentType = PaymentType.Prepaid
        });

        // Assign Agent 1 to Order 1
        await assignmentService.ManualAssignAgentAsync(o1.Id, 1, 1);

        var controller = new OrdersController(orderService, assignmentService, statusService, recoveryService, db);
        SetControllerUser(controller, 1, UserRole.Admin, "admin@delivery.com");

        // Filter by Zone 1
        var zone1Orders = await controller.GetOrders(null, null, zoneId: 1, null, null);
        var list1 = Assert.IsAssignableFrom<IEnumerable<OrderSummaryResponse>>(Assert.IsType<OkObjectResult>(zone1Orders.Result).Value);
        Assert.Single(list1);
        Assert.Equal(o1.Id, list1.First().Id);

        // Filter by Agent 1
        var agent1Orders = await controller.GetOrders(null, null, null, agentId: 1, null);
        var list2 = Assert.IsAssignableFrom<IEnumerable<OrderSummaryResponse>>(Assert.IsType<OkObjectResult>(agent1Orders.Result).Value);
        Assert.Single(list2);
        Assert.Equal(o1.Id, list2.First().Id);

        // Filter by Status Created
        var createdOrders = await controller.GetOrders(null, status: OrderStatus.Created, null, null, null);
        var list3 = Assert.IsAssignableFrom<IEnumerable<OrderSummaryResponse>>(Assert.IsType<OkObjectResult>(createdOrders.Result).Value);
        Assert.Equal(2, list3.Count());
    }
}
