using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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

public class CustomerPhoneNumberTests
{
    private AppDbContext GetInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var context = new AppDbContext(options);

        // Seed Users with distinct phone numbers
        var admin = new User { Id = 1, FullName = "Admin User", Email = "admin@delivery.com", PhoneNumber = "+18005550100", PasswordHash = "dev", Role = UserRole.Admin };
        var custA = new User { Id = 2, FullName = "Customer Alice", Email = "alice@delivery.com", PhoneNumber = "+919037350801", PasswordHash = "dev", Role = UserRole.Customer };
        var custB = new User { Id = 3, FullName = "Customer Bob", Email = "bob@delivery.com", PhoneNumber = "+919037350802", PasswordHash = "dev", Role = UserRole.Customer };
        var agent1 = new User { Id = 101, FullName = "Agent One", Email = "agent1@delivery.com", PhoneNumber = "+919037350803", PasswordHash = "dev", Role = UserRole.Agent };
        var agent2 = new User { Id = 102, FullName = "Agent Two", Email = "agent2@delivery.com", PhoneNumber = "+919037350804", PasswordHash = "dev", Role = UserRole.Agent };
        context.Users.AddRange(admin, custA, custB, agent1, agent2);

        // Seed Zones & Areas
        var zone1 = new Zone { Id = 1, Name = "South Mumbai", Code = "ZONE_A" };
        var zone2 = new Zone { Id = 2, Name = "North Mumbai", Code = "ZONE_B" };
        context.Zones.AddRange(zone1, zone2);

        var area1 = new Area { Id = 1, Name = "Colaba", Code = "COLABA", ZoneId = 1 };
        var area2 = new Area { Id = 2, Name = "Bandra", Code = "BANDRA", ZoneId = 2 };
        context.Areas.AddRange(area1, area2);

        // Seed Agents
        context.Agents.AddRange(
            new Agent { Id = 1, UserId = 101, ZoneId = 1, IsAvailable = true, Latitude = 18.9220, Longitude = 72.8347 },
            new Agent { Id = 2, UserId = 102, ZoneId = 2, IsAvailable = true, Latitude = 19.0596, Longitude = 72.8295 }
        );

        // Seed Rate Cards
        context.RateCards.Add(new RateCard { Id = 1, OrderType = OrderType.B2C, IntraZoneRatePerKg = 40.00m, InterZoneRatePerKg = 60.00m, CODSurcharge = 40.00m });

        context.SaveChanges();
        return context;
    }

    private class RecordingSmsProvider : ISmsNotificationProvider
    {
        public List<(string Phone, string Message, string EventType, int? OrderId)> Dispatches { get; } = new();

        public Task<CommunicationResult> SendSmsAsync(string recipientPhone, string message, string eventType, int? orderId = null)
        {
            Dispatches.Add((recipientPhone, message, eventType, orderId));
            return Task.FromResult(CommunicationResult.Ok("RecordingSmsProvider", Guid.NewGuid().ToString("N")[..8]));
        }
    }

    [Fact]
    public async Task A_RegistrationWithoutPhone_IsRejected()
    {
        var db = GetInMemoryDbContext(nameof(A_RegistrationWithoutPhone_IsRejected));
        var inMemoryConfig = new Dictionary<string, string?> { { "Jwt:SecretKey", "TestKeyForAuthentication1234567890123" } };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
        var authService = new AuthService(db, config);

        var request = new RegisterRequest
        {
            FullName = "No Phone User",
            Email = "nophone@delivery.com",
            Password = "Password@123",
            PhoneNumber = "" // Empty phone number
        };

        await Assert.ThrowsAsync<ArgumentException>(() => authService.RegisterAsync(request));
    }

    [Fact]
    public async Task B_RegistrationWithValidPhone_SucceedsAndPersistsPhone()
    {
        var db = GetInMemoryDbContext(nameof(B_RegistrationWithValidPhone_SucceedsAndPersistsPhone));
        var inMemoryConfig = new Dictionary<string, string?> { { "Jwt:SecretKey", "TestKeyForAuthentication1234567890123" } };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
        var authService = new AuthService(db, config);

        var request = new RegisterRequest
        {
            FullName = "Valid Phone User",
            Email = "validphone@delivery.com",
            Password = "Password@123",
            PhoneNumber = "+919037350803"
        };

        var response = await authService.RegisterAsync(request);

        Assert.NotNull(response);
        Assert.Equal("+919037350803", response.User.PhoneNumber);

        var userInDb = await db.Users.FirstOrDefaultAsync(u => u.Email == "validphone@delivery.com");
        Assert.NotNull(userInDb);
        Assert.Equal("+919037350803", userInDb.PhoneNumber);
    }

    [Fact]
    public async Task C_PhoneNumberStoredAgainstCorrectUser()
    {
        var db = GetInMemoryDbContext(nameof(C_PhoneNumberStoredAgainstCorrectUser));
        var userAlice = await db.Users.FindAsync(2);
        var userBob = await db.Users.FindAsync(3);

        Assert.NotNull(userAlice);
        Assert.NotNull(userBob);
        Assert.Equal("+919037350801", userAlice.PhoneNumber);
        Assert.Equal("+919037350802", userBob.PhoneNumber);
    }

    [Fact]
    public async Task D_CustomerA_Phone_IsNeverUsedForCustomerB()
    {
        var db = GetInMemoryDbContext(nameof(D_CustomerA_Phone_IsNeverUsedForCustomerB));
        var emailProvider = new DevelopmentEmailProvider(NullLogger<DevelopmentEmailProvider>.Instance);
        var smsProvider = new RecordingSmsProvider();
        var notifService = new NotificationService(db, emailProvider, smsProvider);

        // Trigger notification for Customer A (Alice, ID: 2, Phone: +919037350801)
        await notifService.NotifyCustomerAsync(2, 100, "LM-ORD-A", "OrderCreated", "Order Created", "Created for Alice");

        // Trigger notification for Customer B (Bob, ID: 3, Phone: +919037350802)
        await notifService.NotifyCustomerAsync(3, 200, "LM-ORD-B", "OrderCreated", "Order Created", "Created for Bob");

        Assert.Equal(2, smsProvider.Dispatches.Count);
        Assert.Equal("+919037350801", smsProvider.Dispatches[0].Phone);
        Assert.Equal("+919037350802", smsProvider.Dispatches[1].Phone);
    }

    [Fact]
    public async Task E_FailureNotification_ResolvesCustomerStoredPhone()
    {
        var db = GetInMemoryDbContext(nameof(E_FailureNotification_ResolvesCustomerStoredPhone));
        var emailProvider = new DevelopmentEmailProvider(NullLogger<DevelopmentEmailProvider>.Instance);
        var smsProvider = new RecordingSmsProvider();
        var notifService = new NotificationService(db, emailProvider, smsProvider);
        var statusService = new OrderStatusService(db, notifService);

        var order = new Order
        {
            Id = 300,
            TrackingNumber = "LM-ORD-FAIL",
            CustomerId = 2, // Alice
            PickupAreaId = 1,
            DropAreaId = 2,
            Status = OrderStatus.OutForDelivery,
            AssignedAgentId = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        await statusService.UpdateOrderStatusAsync(300, new UpdateOrderStatusRequest
        {
            Status = OrderStatus.Failed,
            ActorId = 101,
            Notes = "Customer not reachable"
        });

        var failSms = smsProvider.Dispatches.FirstOrDefault(d => d.OrderId == 300 && d.EventType == "DeliveryFailed");
        Assert.NotNull(failSms.Phone);
        Assert.Equal("+919037350801", failSms.Phone);
    }

    [Fact]
    public async Task F_RescheduleNotification_ResolvesCustomerStoredPhone()
    {
        var db = GetInMemoryDbContext(nameof(F_RescheduleNotification_ResolvesCustomerStoredPhone));
        var emailProvider = new DevelopmentEmailProvider(NullLogger<DevelopmentEmailProvider>.Instance);
        var smsProvider = new RecordingSmsProvider();
        var notifService = new NotificationService(db, emailProvider, smsProvider);
        var agentAssignmentService = new AgentAssignmentService(db, notifService);
        var recoveryService = new DeliveryRecoveryService(db, agentAssignmentService, notifService);

        var order = new Order
        {
            Id = 400,
            TrackingNumber = "LM-ORD-RESCHED",
            CustomerId = 3, // Bob (Phone: +919037350802)
            PickupAreaId = 1,
            DropAreaId = 2,
            Status = OrderStatus.Failed,
            AssignedAgentId = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        await recoveryService.RescheduleOrderAsync(400, new RescheduleOrderRequest
        {
            CustomerId = 3,
            RescheduledDate = DateTime.UtcNow.AddDays(2),
            Notes = "Rescheduled by Bob"
        });

        var reschedSms = smsProvider.Dispatches.FirstOrDefault(d => d.OrderId == 400 && d.EventType == "OrderRescheduled");
        Assert.NotNull(reschedSms.Phone);
        Assert.Equal("+919037350802", reschedSms.Phone);
    }

    [Fact]
    public async Task G_Notification_RecipientPhone_IsPopulatedInDatabase()
    {
        var db = GetInMemoryDbContext(nameof(G_Notification_RecipientPhone_IsPopulatedInDatabase));
        var emailProvider = new DevelopmentEmailProvider(NullLogger<DevelopmentEmailProvider>.Instance);
        var smsProvider = new RecordingSmsProvider();
        var notifService = new NotificationService(db, emailProvider, smsProvider);

        await notifService.NotifyCustomerAsync(2, 500, "LM-DB-TEST", "OrderCreated", "Order Title", "Order Body");

        var smsNotification = await db.Notifications.FirstOrDefaultAsync(n => n.OrderId == 500 && n.Channel == CommunicationChannel.Sms);
        Assert.NotNull(smsNotification);
        Assert.Equal("+919037350801", smsNotification.RecipientPhone);
    }

    [Fact]
    public async Task H_ExistingAuthentication_StillWorks()
    {
        var db = GetInMemoryDbContext(nameof(H_ExistingAuthentication_StillWorks));
        var inMemoryConfig = new Dictionary<string, string?> { { "Jwt:SecretKey", "TestKeyForAuthentication1234567890123" } };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
        var authService = new AuthService(db, config);

        var reg = await authService.RegisterAsync(new RegisterRequest
        {
            FullName = "New Customer",
            Email = "newcust@delivery.com",
            Password = "SecurePassword@123",
            PhoneNumber = "+919037350803"
        });

        var login = await authService.LoginAsync(new LoginRequest
        {
            Email = "newcust@delivery.com",
            Password = "SecurePassword@123"
        });

        Assert.NotNull(login.Token);
        Assert.Equal("+919037350803", login.User.PhoneNumber);
    }

    [Fact]
    public async Task I_AcceptedTwilioRequest_RecordsDeliveryStatusSent()
    {
        var db = GetInMemoryDbContext(nameof(I_AcceptedTwilioRequest_RecordsDeliveryStatusSent));
        var emailProvider = new DevelopmentEmailProvider(NullLogger<DevelopmentEmailProvider>.Instance);
        var mockSmsProvider = new MockSmsProvider(success: true, providerName: "TwilioSmsProvider");
        var notifService = new NotificationService(db, emailProvider, mockSmsProvider);

        await notifService.NotifyCustomerAsync(2, 601, "LM-TW-SENT", "OrderCreated", "Order Created", "Your order has been placed.");

        var smsNotification = await db.Notifications.FirstOrDefaultAsync(n => n.OrderId == 601 && n.Channel == CommunicationChannel.Sms);
        Assert.NotNull(smsNotification);
        Assert.Equal(CommunicationStatus.Sent, smsNotification.DeliveryStatus);
        Assert.Equal("+919037350801", smsNotification.RecipientPhone);
        Assert.Null(smsNotification.ErrorMessage);
    }

    [Fact]
    public async Task J_RejectedTwilioRequest_RecordsDeliveryStatusFailed()
    {
        var db = GetInMemoryDbContext(nameof(J_RejectedTwilioRequest_RecordsDeliveryStatusFailed));
        var emailProvider = new DevelopmentEmailProvider(NullLogger<DevelopmentEmailProvider>.Instance);
        var mockSmsProvider = new MockSmsProvider(success: false, providerName: "TwilioSmsProvider", error: "HTTP 400: {\"code\":572006,\"message\":\"Invalid template name. Trial accounts can only use predefined SMS templates.\"}");
        var notifService = new NotificationService(db, emailProvider, mockSmsProvider);

        await notifService.NotifyCustomerAsync(2, 602, "LM-TW-FAIL", "OrderCreated", "Order Created", "Your order has been placed.");

        var smsNotification = await db.Notifications.FirstOrDefaultAsync(n => n.OrderId == 602 && n.Channel == CommunicationChannel.Sms);
        Assert.NotNull(smsNotification);
        Assert.Equal(CommunicationStatus.Failed, smsNotification.DeliveryStatus);
        Assert.Equal("+919037350801", smsNotification.RecipientPhone);
        Assert.Contains("572006", smsNotification.ErrorMessage ?? "Invalid template name");
    }

    private class MockSmsProvider : ISmsNotificationProvider
    {
        private readonly bool _success;
        private readonly string _providerName;
        private readonly string? _error;

        public MockSmsProvider(bool success, string providerName, string? error = null)
        {
            _success = success;
            _providerName = providerName;
            _error = error;
        }

        public Task<CommunicationResult> SendSmsAsync(string recipientPhone, string message, string eventType, int? orderId = null)
        {
            return Task.FromResult(_success
                ? CommunicationResult.Ok(_providerName, "SM_MOCK_123456")
                : CommunicationResult.Fail(_providerName, _error ?? "Provider rejected"));
        }
    }
}
