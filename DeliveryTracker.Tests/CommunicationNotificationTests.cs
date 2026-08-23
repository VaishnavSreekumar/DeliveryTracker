using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using DeliveryTracker.API.Controllers;
using DeliveryTracker.API.Data;
using DeliveryTracker.API.DTOs;
using DeliveryTracker.API.Entities;
using DeliveryTracker.API.Enums;
using DeliveryTracker.API.Services;
using DeliveryTracker.API.Services.Communication;
using Xunit;

namespace DeliveryTracker.Tests;

public class CommunicationNotificationTests
{
    private AppDbContext GetInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var context = new AppDbContext(options);

        // Seed Users
        var adminUser = new User { Id = 1, FullName = "Admin User", Email = "admin@delivery.com", PasswordHash = "dev", Role = UserRole.Admin };
        var custUser = new User { Id = 2, FullName = "Customer Alice", Email = "alice@delivery.com", PasswordHash = "dev", Role = UserRole.Customer };
        var otherCust = new User { Id = 3, FullName = "Customer Bob", Email = "bob@delivery.com", PasswordHash = "dev", Role = UserRole.Customer };
        var agentUser = new User { Id = 101, FullName = "Agent One", Email = "agent1@delivery.com", PasswordHash = "dev", Role = UserRole.Agent };
        context.Users.AddRange(adminUser, custUser, otherCust, agentUser);

        // Seed Zones & Areas
        var zone1 = new Zone { Id = 1, Name = "South Mumbai", Code = "ZONE_A" };
        context.Zones.Add(zone1);

        var area1 = new Area { Id = 1, Name = "Colaba", Code = "COLABA", ZoneId = 1 };
        context.Areas.Add(area1);

        // Seed Agent
        var agent1 = new Agent { Id = 1, UserId = 101, ZoneId = 1, IsAvailable = true, Latitude = 18.9220, Longitude = 72.8347 };
        context.Agents.Add(agent1);

        // Seed Rate Cards
        var b2cRateCard = new RateCard { Id = 1, OrderType = OrderType.B2C, IntraZoneRatePerKg = 40.00m, InterZoneRatePerKg = 60.00m, CODSurcharge = 40.00m };
        context.RateCards.Add(b2cRateCard);

        context.SaveChanges();
        return context;
    }

    private class FailingEmailProvider : IEmailNotificationProvider
    {
        public Task<CommunicationResult> SendEmailAsync(string recipientEmail, string subject, string body, string eventType, int? orderId = null)
        {
            throw new HttpRequestException("Simulated external SMTP connection failure!");
        }
    }

    private class FailingSmsProvider : ISmsNotificationProvider
    {
        public Task<CommunicationResult> SendSmsAsync(string recipientPhone, string message, string eventType, int? orderId = null)
        {
            throw new HttpRequestException("Simulated Twilio gateway timeout!");
        }
    }

    private class LiveSuccessfulEmailProvider : IEmailNotificationProvider
    {
        public Task<CommunicationResult> SendEmailAsync(string recipientEmail, string subject, string body, string eventType, int? orderId = null)
        {
            return Task.FromResult(CommunicationResult.Ok("SmtpEmailProvider", "MSG-12345"));
        }
    }

    private class LiveSuccessfulSmsProvider : ISmsNotificationProvider
    {
        public Task<CommunicationResult> SendSmsAsync(string recipientPhone, string message, string eventType, int? orderId = null)
        {
            return Task.FromResult(CommunicationResult.Ok("TwilioSmsProvider", "SM-67890"));
        }
    }

    [Fact]
    public async Task Test1_OrderCreated_TriggersMultiChannelNotifications()
    {
        var db = GetInMemoryDbContext(nameof(Test1_OrderCreated_TriggersMultiChannelNotifications));
        var emailProvider = new DevelopmentEmailProvider(NullLogger<DevelopmentEmailProvider>.Instance);
        var smsProvider = new DevelopmentSmsProvider(NullLogger<DevelopmentSmsProvider>.Instance);
        var notifService = new NotificationService(db, emailProvider, smsProvider);
        var pricingService = new PricingService(db);
        var orderService = new OrderService(db, pricingService, notifService);

        var order = await orderService.CreateOrderAsync(new CreateOrderRequest
        {
            CustomerId = 2,
            PickupAreaId = 1,
            DropAreaId = 1,
            PickupAddress = "Colaba 1",
            DropAddress = "Colaba 2",
            Length = 10, Breadth = 10, Height = 10, ActualWeight = 2.0m,
            OrderType = OrderType.B2C, PaymentType = PaymentType.Prepaid
        });

        var logs = await db.Notifications.Where(n => n.OrderId == order.Id).ToListAsync();
        Assert.Contains(logs, n => n.Channel == CommunicationChannel.InApp && n.EventType == "OrderCreated");
        Assert.Contains(logs, n => n.Channel == CommunicationChannel.Email && n.EventType == "OrderCreated");
        Assert.Contains(logs, n => n.Channel == CommunicationChannel.Sms && n.EventType == "OrderCreated");
    }

    [Fact]
    public async Task Test2_OutForDelivery_And_Delivered_TriggerMultiChannelCommunications()
    {
        var db = GetInMemoryDbContext(nameof(Test2_OutForDelivery_And_Delivered_TriggerMultiChannelCommunications));
        var emailProvider = new DevelopmentEmailProvider(NullLogger<DevelopmentEmailProvider>.Instance);
        var smsProvider = new DevelopmentSmsProvider(NullLogger<DevelopmentSmsProvider>.Instance);
        var notifService = new NotificationService(db, emailProvider, smsProvider);
        var statusService = new OrderStatusService(db, notifService);

        var order = new Order
        {
            Id = 10,
            TrackingNumber = "LM-TEST-001",
            CustomerId = 2,
            PickupAreaId = 1,
            DropAreaId = 1,
            Status = OrderStatus.InTransit,
            AssignedAgentId = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        // 1. Advance to OutForDelivery
        await statusService.UpdateOrderStatusAsync(10, new UpdateOrderStatusRequest
        {
            Status = OrderStatus.OutForDelivery,
            ActorId = 101,
            Notes = "Loaded onto van"
        });

        var outForDeliveryLogs = await db.Notifications
            .Where(n => n.OrderId == 10 && n.EventType == "OutForDelivery")
            .ToListAsync();
        Assert.Contains(outForDeliveryLogs, n => n.Channel == CommunicationChannel.InApp);
        Assert.Contains(outForDeliveryLogs, n => n.Channel == CommunicationChannel.Email);
        Assert.Contains(outForDeliveryLogs, n => n.Channel == CommunicationChannel.Sms);

        // 2. Advance to Delivered
        await statusService.UpdateOrderStatusAsync(10, new UpdateOrderStatusRequest
        {
            Status = OrderStatus.Delivered,
            ActorId = 101,
            Notes = "Delivered to recipient"
        });

        var deliveredLogs = await db.Notifications
            .Where(n => n.OrderId == 10 && n.EventType == "OrderDelivered")
            .ToListAsync();
        Assert.Contains(deliveredLogs, n => n.Channel == CommunicationChannel.InApp);
        Assert.Contains(deliveredLogs, n => n.Channel == CommunicationChannel.Email);
        Assert.Contains(deliveredLogs, n => n.Channel == CommunicationChannel.Sms);
    }

    [Fact]
    public async Task Test3_ExternalProviderFailure_DoesNotFail_OrderStatusUpdate()
    {
        var db = GetInMemoryDbContext(nameof(Test3_ExternalProviderFailure_DoesNotFail_OrderStatusUpdate));
        var failingEmail = new FailingEmailProvider();
        var failingSms = new FailingSmsProvider();
        var notifService = new NotificationService(db, failingEmail, failingSms);
        var statusService = new OrderStatusService(db, notifService);

        var order = new Order
        {
            Id = 20,
            TrackingNumber = "LM-TEST-FAILSAFE",
            CustomerId = 2,
            PickupAreaId = 1,
            DropAreaId = 1,
            Status = OrderStatus.InTransit,
            AssignedAgentId = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var response = await statusService.UpdateOrderStatusAsync(20, new UpdateOrderStatusRequest
        {
            Status = OrderStatus.OutForDelivery,
            ActorId = 101,
            Notes = "Advancing with offline providers"
        });

        Assert.Equal(OrderStatus.OutForDelivery, response.CurrentStatus);

        var emailLog = await db.Notifications.FirstOrDefaultAsync(n => n.OrderId == 20 && n.Channel == CommunicationChannel.Email);
        Assert.NotNull(emailLog);
        Assert.Equal(CommunicationStatus.Failed, emailLog.DeliveryStatus);
        Assert.Contains("Simulated external SMTP connection failure", emailLog.ErrorMessage);

        var smsLog = await db.Notifications.FirstOrDefaultAsync(n => n.OrderId == 20 && n.Channel == CommunicationChannel.Sms);
        Assert.NotNull(smsLog);
        Assert.Equal(CommunicationStatus.Failed, smsLog.DeliveryStatus);
        Assert.Contains("Simulated Twilio gateway timeout", smsLog.ErrorMessage);
    }

    [Fact]
    public async Task Test4_LiveProvider_RecordsSentStatus_And_SimulatedProvider_RecordsSimulatedStatus()
    {
        var db = GetInMemoryDbContext(nameof(Test4_LiveProvider_RecordsSentStatus_And_SimulatedProvider_RecordsSimulatedStatus));
        // Real provider test
        var liveEmail = new LiveSuccessfulEmailProvider();
        var liveSms = new LiveSuccessfulSmsProvider();
        var liveNotifService = new NotificationService(db, liveEmail, liveSms);

        await liveNotifService.NotifyCustomerAsync(2, 30, "LM-LIVE-001", "OrderCreated", "Order Placed", "Your order was placed");

        var emailSentLog = await db.Notifications.FirstOrDefaultAsync(n => n.OrderId == 30 && n.Channel == CommunicationChannel.Email);
        Assert.NotNull(emailSentLog);
        Assert.Equal(CommunicationStatus.Sent, emailSentLog.DeliveryStatus);

        var smsSentLog = await db.Notifications.FirstOrDefaultAsync(n => n.OrderId == 30 && n.Channel == CommunicationChannel.Sms);
        Assert.NotNull(smsSentLog);
        Assert.Equal(CommunicationStatus.Sent, smsSentLog.DeliveryStatus);

        // Simulation provider test
        var devEmail = new DevelopmentEmailProvider(NullLogger<DevelopmentEmailProvider>.Instance);
        var devSms = new DevelopmentSmsProvider(NullLogger<DevelopmentSmsProvider>.Instance);
        var devNotifService = new NotificationService(db, devEmail, devSms);

        await devNotifService.NotifyCustomerAsync(2, 31, "LM-DEV-001", "OrderCreated", "Order Placed", "Your order was placed");

        var emailSimLog = await db.Notifications.FirstOrDefaultAsync(n => n.OrderId == 31 && n.Channel == CommunicationChannel.Email);
        Assert.NotNull(emailSimLog);
        Assert.Equal(CommunicationStatus.Simulated, emailSimLog.DeliveryStatus);

        var smsSimLog = await db.Notifications.FirstOrDefaultAsync(n => n.OrderId == 31 && n.Channel == CommunicationChannel.Sms);
        Assert.NotNull(smsSimLog);
        Assert.Equal(CommunicationStatus.Simulated, smsSimLog.DeliveryStatus);
    }

    [Fact]
    public async Task Test5_CustomerIsolation_CustomerOnlySeesOwnInAppNotifications()
    {
        var db = GetInMemoryDbContext(nameof(Test5_CustomerIsolation_CustomerOnlySeesOwnInAppNotifications));
        var emailProvider = new DevelopmentEmailProvider(NullLogger<DevelopmentEmailProvider>.Instance);
        var smsProvider = new DevelopmentSmsProvider(NullLogger<DevelopmentSmsProvider>.Instance);
        var notifService = new NotificationService(db, emailProvider, smsProvider);

        // Seed orders
        db.Orders.AddRange(
            new Order { Id = 1, TrackingNumber = "LM-ORDER-1", CustomerId = 2, PickupAreaId = 1, DropAreaId = 1 },
            new Order { Id = 2, TrackingNumber = "LM-ORDER-2", CustomerId = 3, PickupAreaId = 1, DropAreaId = 1 }
        );

        // Seed notifications for User 2 (Alice) and User 3 (Bob)
        db.Notifications.AddRange(
            new Notification { Id = 101, UserId = 2, OrderId = 1, Title = "Alice Note 1", Channel = CommunicationChannel.InApp, SentAt = DateTime.UtcNow },
            new Notification { Id = 102, UserId = 2, OrderId = 1, Title = "Alice Note 2", Channel = CommunicationChannel.InApp, SentAt = DateTime.UtcNow },
            new Notification { Id = 103, UserId = 3, OrderId = 2, Title = "Bob Secret Note", Channel = CommunicationChannel.InApp, SentAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var aliceNotifications = (await notifService.GetCustomerNotificationsAsync(2)).ToList();
        Assert.Equal(2, aliceNotifications.Count);
        Assert.All(aliceNotifications, n => Assert.Equal(2, n.UserId));
        Assert.DoesNotContain(aliceNotifications, n => n.Title == "Bob Secret Note");
    }

    [Fact]
    public async Task Test6_AdminCanInspect_OrderCommunicationLogs()
    {
        var db = GetInMemoryDbContext(nameof(Test6_AdminCanInspect_OrderCommunicationLogs));
        var emailProvider = new DevelopmentEmailProvider(NullLogger<DevelopmentEmailProvider>.Instance);
        var smsProvider = new DevelopmentSmsProvider(NullLogger<DevelopmentSmsProvider>.Instance);
        var notifService = new NotificationService(db, emailProvider, smsProvider);

        var controller = new NotificationsController(db, notifService);

        db.Orders.Add(new Order { Id = 50, TrackingNumber = "LM-ORDER-50", CustomerId = 2, PickupAreaId = 1, DropAreaId = 1 });
        db.Notifications.AddRange(
            new Notification { Id = 1, UserId = 2, OrderId = 50, Title = "InApp Note", Channel = CommunicationChannel.InApp, SentAt = DateTime.UtcNow },
            new Notification { Id = 2, UserId = 2, OrderId = 50, Title = "Email Note", Channel = CommunicationChannel.Email, SentAt = DateTime.UtcNow },
            new Notification { Id = 3, UserId = 2, OrderId = 50, Title = "Sms Note", Channel = CommunicationChannel.Sms, SentAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var result = await controller.GetOrderCommunications(50);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var logs = Assert.IsAssignableFrom<IEnumerable<NotificationDto>>(okResult.Value);

        Assert.Equal(3, logs.Count());
    }
}
