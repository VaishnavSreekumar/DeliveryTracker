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

public class NotificationTests
{
    private AppDbContext GetInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var context = new AppDbContext(options);

        // Seed Users: Customer A (Id 2), Customer B (Id 3), Agent 1 (Id 101), Admin (Id 1)
        var customerA = new User { Id = 2, FullName = "Customer A", Email = "customera@delivery.com", PasswordHash = "dev", Role = UserRole.Customer };
        var customerB = new User { Id = 3, FullName = "Customer B", Email = "customerb@delivery.com", PasswordHash = "dev", Role = UserRole.Customer };
        var agent1User = new User { Id = 101, FullName = "Raj Agent", Email = "agent1@delivery.com", PasswordHash = "dev", Role = UserRole.Agent };
        var agent2User = new User { Id = 102, FullName = "Vikram Agent", Email = "agent2@delivery.com", PasswordHash = "dev", Role = UserRole.Agent };
        var adminUser = new User { Id = 1, FullName = "Admin User", Email = "admin@delivery.com", PasswordHash = "dev", Role = UserRole.Admin };
        context.Users.AddRange(customerA, customerB, agent1User, agent2User, adminUser);

        // Seed Zone & Area
        var zoneA = new Zone { Id = 1, Name = "Zone A", Code = "ZONE_A" };
        context.Zones.Add(zoneA);

        var colaba = new Area { Id = 1, Name = "Colaba", Code = "COLABA", ZoneId = 1 };
        context.Areas.Add(colaba);

        // Seed Agents
        var agent1 = new Agent { Id = 1, UserId = 101, ZoneId = 1, IsAvailable = false, Latitude = 18.9220, Longitude = 72.8347 };
        var agent2 = new Agent { Id = 2, UserId = 102, ZoneId = 1, IsAvailable = true, Latitude = 18.9220, Longitude = 72.8347 };
        context.Agents.AddRange(agent1, agent2);

        // Seed Rate Card
        var rateCard = new RateCard { Id = 1, OrderType = OrderType.B2C, IntraZoneRatePerKg = 40, InterZoneRatePerKg = 60, CODSurcharge = 40 };
        context.RateCards.Add(rateCard);

        context.SaveChanges();
        return context;
    }

    private void SetControllerUser(ControllerBase controller, int userId, UserRole role, string email)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
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
    public async Task Test1_FailedDelivery_CreatesNotification_WithUserIdAndTitle()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test1_FailedDelivery_CreatesNotification_WithUserIdAndTitle));
        var order = new Order
        {
            Id = 1,
            TrackingNumber = "LM-FAIL-NOTIF-001",
            CustomerId = 2, // Customer A
            PickupAreaId = 1,
            DropAreaId = 1,
            Status = OrderStatus.OutForDelivery,
            AssignedAgentId = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var statusService = new OrderStatusService(db);

        // Act: Agent 1 marks order Failed
        await statusService.UpdateOrderStatusAsync(1, new UpdateOrderStatusRequest
        {
            Status = OrderStatus.Failed,
            ActorId = 101,
            Notes = "Customer not home"
        });

        // Assert: Notification created with proper UserId, Title, and IsRead=false
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.OrderId == 1);
        Assert.NotNull(notification);
        Assert.Equal(2, notification.UserId);
        Assert.Contains("Delivery Attempt Failed", notification.Title);
        Assert.Contains("LM-FAIL-NOTIF-001", notification.Title);
        Assert.Contains("Customer not home", notification.Message);
        Assert.False(notification.IsRead);
    }

    [Fact]
    public async Task Test2_Reschedule_CreatesNotifications_WithUserIdAndTitle()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test2_Reschedule_CreatesNotifications_WithUserIdAndTitle));
        var order = new Order
        {
            Id = 1,
            TrackingNumber = "LM-RESCHED-NOTIF-001",
            CustomerId = 2, // Customer A
            PickupAreaId = 1,
            DropAreaId = 1,
            Status = OrderStatus.Failed,
            AssignedAgentId = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var assignmentService = new AgentAssignmentService(db);
        var recoveryService = new DeliveryRecoveryService(db, assignmentService);

        // Act: Customer A reschedules
        var futureDate = DateTime.UtcNow.AddDays(2);
        await recoveryService.RescheduleOrderAsync(1, new RescheduleOrderRequest
        {
            CustomerId = 2,
            RescheduledDate = futureDate,
            Notes = "Rescheduled by customer"
        });

        // Assert: Notifications created associated with Customer A
        var notifications = await db.Notifications.Where(n => n.OrderId == 1).ToListAsync();
        Assert.NotEmpty(notifications);
        Assert.All(notifications, n =>
        {
            Assert.Equal(2, n.UserId);
            Assert.False(n.IsRead);
            Assert.Contains("LM-RESCHED-NOTIF-001", n.Title);
        });
    }

    [Fact]
    public async Task Test3_Customer_CanRetrieveOwnNotifications()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test3_Customer_CanRetrieveOwnNotifications));
        var orderA = new Order { Id = 1, TrackingNumber = "LM-ORD-A", CustomerId = 2, PickupAreaId = 1, DropAreaId = 1 };
        var orderB = new Order { Id = 2, TrackingNumber = "LM-ORD-B", CustomerId = 3, PickupAreaId = 1, DropAreaId = 1 };
        db.Orders.AddRange(orderA, orderB);

        db.Notifications.AddRange(
            new Notification { Id = 1, UserId = 2, OrderId = 1, Title = "Notif for A", Message = "Msg A", IsRead = false },
            new Notification { Id = 2, UserId = 3, OrderId = 2, Title = "Notif for B", Message = "Msg B", IsRead = false }
        );
        await db.SaveChangesAsync();

        var controller = new NotificationsController(db);
        SetControllerUser(controller, userId: 2, role: UserRole.Customer, email: "customera@delivery.com");

        // Act
        var result = await controller.GetNotifications();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var notifs = Assert.IsAssignableFrom<IEnumerable<NotificationDto>>(okResult.Value).ToList();
        Assert.Single(notifs);
        Assert.Equal(1, notifs[0].Id);
        Assert.Equal("Notif for A", notifs[0].Title);
        Assert.Equal("LM-ORD-A", notifs[0].OrderTrackingNumber);
    }

    [Fact]
    public async Task Test4_Customer_CannotRetrieveAnotherCustomersNotifications()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test4_Customer_CannotRetrieveAnotherCustomersNotifications));
        var orderB = new Order { Id = 2, TrackingNumber = "LM-ORD-B", CustomerId = 3, PickupAreaId = 1, DropAreaId = 1 };
        db.Orders.Add(orderB);

        db.Notifications.Add(new Notification { Id = 2, UserId = 3, OrderId = 2, Title = "Secret for B", Message = "Msg B", IsRead = false });
        await db.SaveChangesAsync();

        var controller = new NotificationsController(db);
        // Login as Customer A (Id 2)
        SetControllerUser(controller, userId: 2, role: UserRole.Customer, email: "customera@delivery.com");

        // Act
        var result = await controller.GetNotifications();

        // Assert: Customer A sees 0 notifications
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var notifs = Assert.IsAssignableFrom<IEnumerable<NotificationDto>>(okResult.Value).ToList();
        Assert.Empty(notifs);
    }

    [Fact]
    public async Task Test5_MarkAsRead_PersistsIsRead()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test5_MarkAsRead_PersistsIsRead));
        var order = new Order { Id = 1, TrackingNumber = "LM-ORD-A", CustomerId = 2, PickupAreaId = 1, DropAreaId = 1 };
        db.Orders.Add(order);

        db.Notifications.Add(new Notification { Id = 1, UserId = 2, OrderId = 1, Title = "Notif for A", Message = "Msg A", IsRead = false });
        await db.SaveChangesAsync();

        var controller = new NotificationsController(db);
        SetControllerUser(controller, userId: 2, role: UserRole.Customer, email: "customera@delivery.com");

        // Act
        var result = await controller.MarkAsRead(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var notifDto = Assert.IsType<NotificationDto>(okResult.Value);
        Assert.True(notifDto.IsRead);

        // Verify DB persistence
        var dbNotif = await db.Notifications.FindAsync(1);
        Assert.True(dbNotif!.IsRead);
    }

    [Fact]
    public async Task Test6_CustomerCannotMarkAnotherCustomersNotificationAsRead_ReturnsForbidden()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test6_CustomerCannotMarkAnotherCustomersNotificationAsRead_ReturnsForbidden));
        var orderB = new Order { Id = 2, TrackingNumber = "LM-ORD-B", CustomerId = 3, PickupAreaId = 1, DropAreaId = 1 };
        db.Orders.Add(orderB);

        db.Notifications.Add(new Notification { Id = 10, UserId = 3, OrderId = 2, Title = "Notif for B", Message = "Msg B", IsRead = false });
        await db.SaveChangesAsync();

        var controller = new NotificationsController(db);
        // Login as Customer A (Id 2), trying to mark Customer B's notification (Id 10) as read
        SetControllerUser(controller, userId: 2, role: UserRole.Customer, email: "customera@delivery.com");

        // Act
        var result = await controller.MarkAsRead(10);

        // Assert
        Assert.IsType<ForbidResult>(result.Result);

        // Verify DB unchanged
        var dbNotif = await db.Notifications.FindAsync(10);
        Assert.False(dbNotif!.IsRead);
    }

    [Fact]
    public async Task Test7_UnreadCount_IsCorrect()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test7_UnreadCount_IsCorrect));
        var order = new Order { Id = 1, TrackingNumber = "LM-ORD-A", CustomerId = 2, PickupAreaId = 1, DropAreaId = 1 };
        db.Orders.Add(order);

        db.Notifications.AddRange(
            new Notification { Id = 1, UserId = 2, OrderId = 1, Title = "N1", Message = "M1", IsRead = false },
            new Notification { Id = 2, UserId = 2, OrderId = 1, Title = "N2", Message = "M2", IsRead = true },
            new Notification { Id = 3, UserId = 2, OrderId = 1, Title = "N3", Message = "M3", IsRead = false }
        );
        await db.SaveChangesAsync();

        var controller = new NotificationsController(db);
        SetControllerUser(controller, userId: 2, role: UserRole.Customer, email: "customera@delivery.com");

        // Act
        var result = await controller.GetNotifications();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var notifs = Assert.IsAssignableFrom<IEnumerable<NotificationDto>>(okResult.Value).ToList();
        var unreadCount = notifs.Count(n => !n.IsRead);
        Assert.Equal(3, notifs.Count);
        Assert.Equal(2, unreadCount);
    }
}
