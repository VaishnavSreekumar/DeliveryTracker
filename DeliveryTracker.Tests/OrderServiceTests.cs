using Microsoft.EntityFrameworkCore;
using DeliveryTracker.API.Data;
using DeliveryTracker.API.DTOs;
using DeliveryTracker.API.Entities;
using DeliveryTracker.API.Enums;
using DeliveryTracker.API.Services;
using Xunit;

namespace DeliveryTracker.Tests;

public class OrderServiceTests
{
    private AppDbContext GetInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var context = new AppDbContext(options);

        // Seed Customer
        var customerUser = new User
        {
            Id = 2,
            FullName = "John Customer",
            Email = "customer@delivery.com",
            PasswordHash = "dev_hash",
            Role = UserRole.Customer,
            CreatedAt = DateTime.UtcNow
        };
        context.Users.Add(customerUser);

        // Seed Zones & Areas
        var zoneA = new Zone { Id = 1, Name = "Zone A", Code = "ZONE_A" };
        var zoneB = new Zone { Id = 2, Name = "Zone B", Code = "ZONE_B" };
        var zoneC = new Zone { Id = 3, Name = "Zone C", Code = "ZONE_C" };
        context.Zones.AddRange(zoneA, zoneB, zoneC);

        var colaba = new Area { Id = 1, Name = "Colaba", Code = "COLABA", ZoneId = 1 };
        var dadar = new Area { Id = 2, Name = "Dadar", Code = "DADAR", ZoneId = 1 };
        var andheri = new Area { Id = 3, Name = "Andheri", Code = "ANDHERI", ZoneId = 2 };
        var bandra = new Area { Id = 4, Name = "Bandra", Code = "BANDRA", ZoneId = 2 };
        var thane = new Area { Id = 5, Name = "Thane", Code = "THANE", ZoneId = 3 };
        var powai = new Area { Id = 6, Name = "Powai", Code = "POWAI", ZoneId = 3 };
        context.Areas.AddRange(colaba, dadar, andheri, bandra, thane, powai);

        // Seed Rate Cards
        var b2cRateCard = new RateCard
        {
            Id = 1,
            OrderType = OrderType.B2C,
            IntraZoneRatePerKg = 40.00m,
            InterZoneRatePerKg = 60.00m,
            CODSurcharge = 40.00m
        };

        var b2bRateCard = new RateCard
        {
            Id = 2,
            OrderType = OrderType.B2B,
            IntraZoneRatePerKg = 30.00m,
            InterZoneRatePerKg = 50.00m,
            CODSurcharge = 30.00m
        };
        context.RateCards.AddRange(b2cRateCard, b2bRateCard);

        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task Test1_CreateValidB2CCODOrder()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test1_CreateValidB2CCODOrder));
        var pricingService = new PricingService(db);
        var orderService = new OrderService(db, pricingService);

        var request = new CreateOrderRequest
        {
            CustomerId = 2,
            PickupAreaId = 3, // Andheri (Zone B)
            DropAreaId = 5,   // Thane (Zone C)
            Length = 50,
            Breadth = 40,
            Height = 30,
            ActualWeight = 8.0m,
            OrderType = OrderType.B2C,
            PaymentType = PaymentType.COD,
            PickupAddress = "123 Andheri West",
            DropAddress = "456 Thane East"
        };

        // Act
        var result = await orderService.CreateOrderAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.StartsWith("LM-", result.TrackingNumber);
        Assert.Equal(OrderStatus.Created, result.Status);
        Assert.Null(result.AssignedAgentId);
        Assert.Null(result.AssignedAgentName);

        // Pricing verification (Andheri -> Thane, B2C COD, Vol 12kg)
        Assert.Equal(12.00m, result.VolumetricWeight);
        Assert.Equal(12.00m, result.ChargeableWeight);
        Assert.Equal(60.00m, result.RatePerKg);
        Assert.Equal(720.00m, result.DeliveryFee);
        Assert.Equal(40.00m, result.CODSurcharge);
        Assert.Equal(760.00m, result.TotalAmount);

        // Initial status history verification
        Assert.Single(result.StatusHistory);
        var history = result.StatusHistory.First();
        Assert.Equal(OrderStatus.Created, history.Status);
        Assert.Equal(2, history.ActorId);
        Assert.Equal(UserRole.Customer, history.ActorRole);
        Assert.Equal("Order created", history.Notes);

        // Verify DB persistence count
        Assert.Equal(1, await db.Orders.CountAsync());
        Assert.Equal(1, await db.OrderStatusHistories.CountAsync());
    }

    [Fact]
    public async Task Test2_CreateB2CPrepaidOrder()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test2_CreateB2CPrepaidOrder));
        var pricingService = new PricingService(db);
        var orderService = new OrderService(db, pricingService);

        var request = new CreateOrderRequest
        {
            CustomerId = 2,
            PickupAreaId = 3, // Andheri (Zone B)
            DropAreaId = 4,   // Bandra (Zone B)
            Length = 10,
            Breadth = 10,
            Height = 10,
            ActualWeight = 10.0m,
            OrderType = OrderType.B2C,
            PaymentType = PaymentType.Prepaid
        };

        // Act
        var result = await orderService.CreateOrderAsync(request);

        // Assert
        Assert.Equal(0.00m, result.CODSurcharge);
        Assert.Equal(result.DeliveryFee, result.TotalAmount);
        Assert.Equal(400.00m, result.TotalAmount);
    }

    [Fact]
    public async Task Test3_CreateB2BInterZoneCODOrder()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test3_CreateB2BInterZoneCODOrder));
        var pricingService = new PricingService(db);
        var orderService = new OrderService(db, pricingService);

        var request = new CreateOrderRequest
        {
            CustomerId = 2,
            PickupAreaId = 3, // Andheri (Zone B)
            DropAreaId = 5,   // Thane (Zone C)
            Length = 10,
            Breadth = 10,
            Height = 10,
            ActualWeight = 5.0m,
            OrderType = OrderType.B2B,
            PaymentType = PaymentType.COD
        };

        // Act
        var result = await orderService.CreateOrderAsync(request);

        // Assert
        Assert.Equal(50.00m, result.RatePerKg);
        Assert.Equal(250.00m, result.DeliveryFee);
        Assert.Equal(30.00m, result.CODSurcharge);
        Assert.Equal(280.00m, result.TotalAmount);
    }

    [Fact]
    public async Task Test4_InvalidPickupArea_ThrowsKeyNotFoundException()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test4_InvalidPickupArea_ThrowsKeyNotFoundException));
        var pricingService = new PricingService(db);
        var orderService = new OrderService(db, pricingService);

        var request = new CreateOrderRequest
        {
            CustomerId = 2,
            PickupAreaId = 999, // Invalid
            DropAreaId = 5,
            Length = 10,
            Breadth = 10,
            Height = 10,
            ActualWeight = 5.0m,
            OrderType = OrderType.B2C,
            PaymentType = PaymentType.Prepaid
        };

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => orderService.CreateOrderAsync(request));
    }

    [Fact]
    public async Task Test5_InvalidDimensions_ThrowsArgumentException()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test5_InvalidDimensions_ThrowsArgumentException));
        var pricingService = new PricingService(db);
        var orderService = new OrderService(db, pricingService);

        var request = new CreateOrderRequest
        {
            CustomerId = 2,
            PickupAreaId = 3,
            DropAreaId = 5,
            Length = 0, // Invalid dimension
            Breadth = 10,
            Height = 10,
            ActualWeight = 5.0m,
            OrderType = OrderType.B2C,
            PaymentType = PaymentType.Prepaid
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => orderService.CreateOrderAsync(request));
    }

    [Fact]
    public async Task Test6_VerifyCalculatePriceDoesNotCreateOrder()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test6_VerifyCalculatePriceDoesNotCreateOrder));
        var pricingService = new PricingService(db);

        var request = new CalculatePriceRequest
        {
            PickupAreaId = 3,
            DropAreaId = 5,
            Length = 50,
            Breadth = 40,
            Height = 30,
            ActualWeight = 8.0m,
            OrderType = OrderType.B2C,
            PaymentType = PaymentType.COD
        };

        // Act
        var result = await pricingService.CalculatePriceAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, await db.Orders.CountAsync());
        Assert.Equal(0, await db.OrderStatusHistories.CountAsync());
    }

    [Fact]
    public async Task Test7_GetOrders_CustomerScoping_ReturnsOnlyCustomerOrders()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test7_GetOrders_CustomerScoping_ReturnsOnlyCustomerOrders));
        var userB = new User { Id = 3, FullName = "Jane Customer", Email = "jane@delivery.com", PasswordHash = "dev", Role = UserRole.Customer };
        db.Users.Add(userB);

        var orderA = new Order { Id = 1, TrackingNumber = "LM-ORD-A", CustomerId = 2, PickupAreaId = 1, DropAreaId = 2, TotalAmount = 200, Status = OrderStatus.Created };
        var orderB = new Order { Id = 2, TrackingNumber = "LM-ORD-B", CustomerId = 3, PickupAreaId = 1, DropAreaId = 2, TotalAmount = 300, Status = OrderStatus.Created };
        db.Orders.AddRange(orderA, orderB);
        await db.SaveChangesAsync();

        var pricingService = new PricingService(db);
        var orderService = new OrderService(db, pricingService);

        // Act: Get orders for Customer ID 2
        var customerOrders = (await orderService.GetOrdersAsync(customerId: 2)).ToList();

        // Assert
        Assert.Single(customerOrders);
        Assert.Equal("LM-ORD-A", customerOrders[0].TrackingNumber);
        Assert.Equal(2, customerOrders[0].CustomerId);
    }

    [Fact]
    public async Task Test8_GetOrders_AdminGlobalVisibility_ReturnsAllOrders()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test8_GetOrders_AdminGlobalVisibility_ReturnsAllOrders));
        var userB = new User { Id = 3, FullName = "Jane Customer", Email = "jane@delivery.com", PasswordHash = "dev", Role = UserRole.Customer };
        db.Users.Add(userB);

        var orderA = new Order { Id = 1, TrackingNumber = "LM-ORD-A", CustomerId = 2, PickupAreaId = 1, DropAreaId = 2, TotalAmount = 200, Status = OrderStatus.Created };
        var orderB = new Order { Id = 2, TrackingNumber = "LM-ORD-B", CustomerId = 3, PickupAreaId = 1, DropAreaId = 2, TotalAmount = 300, Status = OrderStatus.Created };
        db.Orders.AddRange(orderA, orderB);
        await db.SaveChangesAsync();

        var pricingService = new PricingService(db);
        var orderService = new OrderService(db, pricingService);

        // Act: Get all orders (no customerId passed, admin query)
        var allOrders = (await orderService.GetOrdersAsync(customerId: null)).ToList();

        // Assert
        Assert.Equal(2, allOrders.Count);
    }

    [Fact]
    public async Task Test9_GetOrderById_ReturnsCompleteOrderDetailsWithHistory()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test9_GetOrderById_ReturnsCompleteOrderDetailsWithHistory));
        var order = new Order
        {
            Id = 10,
            TrackingNumber = "LM-DETAIL-001",
            CustomerId = 2,
            PickupAreaId = 1,
            DropAreaId = 3,
            PickupAddress = "Colaba 1",
            DropAddress = "Andheri 2",
            TotalAmount = 500,
            Status = OrderStatus.Created,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Orders.Add(order);
        db.OrderStatusHistories.Add(new OrderStatusHistory
        {
            OrderId = 10,
            Status = OrderStatus.Created,
            ActorId = 2,
            ActorRole = UserRole.Customer,
            Notes = "Order placed",
            Timestamp = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var pricingService = new PricingService(db);
        var orderService = new OrderService(db, pricingService);

        // Act
        var result = await orderService.GetOrderByIdAsync(10);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("LM-DETAIL-001", result.TrackingNumber);
        Assert.Equal("John Customer", result.CustomerName);
        Assert.Equal("Colaba", result.PickupArea);
        Assert.Equal("Andheri", result.DropArea);
        Assert.Single(result.StatusHistory);
    }

    [Fact]
    public async Task Test10_InvalidCustomerId_ThrowsKeyNotFoundException()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test10_InvalidCustomerId_ThrowsKeyNotFoundException));
        var pricingService = new PricingService(db);
        var orderService = new OrderService(db, pricingService);

        var request = new CreateOrderRequest
        {
            CustomerId = 9999, // Non-existent user
            PickupAreaId = 1,
            DropAreaId = 3,
            Length = 10,
            Breadth = 10,
            Height = 10,
            ActualWeight = 2.0m,
            OrderType = OrderType.B2C,
            PaymentType = PaymentType.Prepaid
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => orderService.CreateOrderAsync(request));
        Assert.Contains("Customer with ID 9999 not found", ex.Message);
    }

    [Fact]
    public async Task Test11_InvalidDropArea_ThrowsKeyNotFoundException()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test11_InvalidDropArea_ThrowsKeyNotFoundException));
        var pricingService = new PricingService(db);
        var orderService = new OrderService(db, pricingService);

        var request = new CreateOrderRequest
        {
            CustomerId = 2,
            PickupAreaId = 1,
            DropAreaId = 8888, // Non-existent drop area
            Length = 10,
            Breadth = 10,
            Height = 10,
            ActualWeight = 2.0m,
            OrderType = OrderType.B2C,
            PaymentType = PaymentType.Prepaid
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => orderService.CreateOrderAsync(request));
        Assert.Contains("Drop area with ID 8888 not found", ex.Message);
    }
}
