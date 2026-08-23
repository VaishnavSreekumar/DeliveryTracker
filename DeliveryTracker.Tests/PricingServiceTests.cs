using Microsoft.EntityFrameworkCore;
using DeliveryTracker.API.Data;
using DeliveryTracker.API.DTOs;
using DeliveryTracker.API.Entities;
using DeliveryTracker.API.Enums;
using DeliveryTracker.API.Services;
using Xunit;

namespace DeliveryTracker.Tests;

public class PricingServiceTests
{
    private AppDbContext GetInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var context = new AppDbContext(options);

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
    public async Task Test1_VolumetricWeightGreaterThanActual_B2CIntraZonePrepaid()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test1_VolumetricWeightGreaterThanActual_B2CIntraZonePrepaid));
        var pricingService = new PricingService(db);

        var request = new CalculatePriceRequest
        {
            PickupAreaId = 3, // Andheri (Zone B)
            DropAreaId = 4,   // Bandra (Zone B)
            Length = 50,
            Breadth = 40,
            Height = 30,
            ActualWeight = 8.0m,
            OrderType = OrderType.B2C,
            PaymentType = PaymentType.Prepaid
        };

        // Act
        var result = await pricingService.CalculatePriceAsync(request);

        // Assert
        Assert.True(result.IsIntraZone);
        Assert.Equal(12.00m, result.VolumetricWeight);
        Assert.Equal(12.00m, result.ChargeableWeight);
        Assert.Equal(40.00m, result.RatePerKg);
        Assert.Equal(480.00m, result.DeliveryFee);
        Assert.Equal(0.00m, result.CODSurcharge);
        Assert.Equal(480.00m, result.TotalAmount);
    }

    [Fact]
    public async Task Test2_ActualWeightGreaterThanVolumetric_B2CIntraZonePrepaid()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test2_ActualWeightGreaterThanVolumetric_B2CIntraZonePrepaid));
        var pricingService = new PricingService(db);

        var request = new CalculatePriceRequest
        {
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
        var result = await pricingService.CalculatePriceAsync(request);

        // Assert
        Assert.True(result.IsIntraZone);
        Assert.Equal(0.20m, result.VolumetricWeight);
        Assert.Equal(10.00m, result.ChargeableWeight);
        Assert.Equal(40.00m, result.RatePerKg);
        Assert.Equal(400.00m, result.DeliveryFee);
        Assert.Equal(0.00m, result.CODSurcharge);
        Assert.Equal(400.00m, result.TotalAmount);
    }

    [Fact]
    public async Task Test3_B2CInterZoneCOD()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test3_B2CInterZoneCOD));
        var pricingService = new PricingService(db);

        var request = new CalculatePriceRequest
        {
            PickupAreaId = 3, // Andheri (Zone B)
            DropAreaId = 5,   // Thane (Zone C)
            Length = 10,
            Breadth = 10,
            Height = 10,
            ActualWeight = 5.0m,
            OrderType = OrderType.B2C,
            PaymentType = PaymentType.COD
        };

        // Act
        var result = await pricingService.CalculatePriceAsync(request);

        // Assert
        Assert.False(result.IsIntraZone);
        Assert.Equal(5.00m, result.ChargeableWeight);
        Assert.Equal(60.00m, result.RatePerKg);
        Assert.Equal(300.00m, result.DeliveryFee);
        Assert.Equal(40.00m, result.CODSurcharge);
        Assert.Equal(340.00m, result.TotalAmount);
    }

    [Fact]
    public async Task Test4_B2BIntraZonePrepaid()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test4_B2BIntraZonePrepaid));
        var pricingService = new PricingService(db);

        var request = new CalculatePriceRequest
        {
            PickupAreaId = 3, // Andheri (Zone B)
            DropAreaId = 4,   // Bandra (Zone B)
            Length = 10,
            Breadth = 10,
            Height = 10,
            ActualWeight = 5.0m,
            OrderType = OrderType.B2B,
            PaymentType = PaymentType.Prepaid
        };

        // Act
        var result = await pricingService.CalculatePriceAsync(request);

        // Assert
        Assert.True(result.IsIntraZone);
        Assert.Equal(5.00m, result.ChargeableWeight);
        Assert.Equal(30.00m, result.RatePerKg);
        Assert.Equal(150.00m, result.DeliveryFee);
        Assert.Equal(0.00m, result.CODSurcharge);
        Assert.Equal(150.00m, result.TotalAmount);
    }

    [Fact]
    public async Task Test5_B2BInterZoneCOD()
    {
        // Arrange
        var db = GetInMemoryDbContext(nameof(Test5_B2BInterZoneCOD));
        var pricingService = new PricingService(db);

        var request = new CalculatePriceRequest
        {
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
        var result = await pricingService.CalculatePriceAsync(request);

        // Assert
        Assert.False(result.IsIntraZone);
        Assert.Equal(5.00m, result.ChargeableWeight);
        Assert.Equal(50.00m, result.RatePerKg);
        Assert.Equal(250.00m, result.DeliveryFee);
        Assert.Equal(30.00m, result.CODSurcharge);
        Assert.Equal(280.00m, result.TotalAmount);
    }
}
