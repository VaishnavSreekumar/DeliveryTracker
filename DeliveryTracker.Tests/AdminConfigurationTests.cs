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

public class AdminConfigurationTests
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
        var customerUser = new User { Id = 2, FullName = "Customer User", Email = "customer@delivery.com", PasswordHash = "dev", Role = UserRole.Customer };
        var agentUser = new User { Id = 101, FullName = "Agent User", Email = "agent@delivery.com", PasswordHash = "dev", Role = UserRole.Agent };
        context.Users.AddRange(adminUser, customerUser, agentUser);

        // Seed Zones & Areas
        var zoneA = new Zone { Id = 1, Name = "South Mumbai", Code = "ZONE_SOUTH" };
        var zoneB = new Zone { Id = 2, Name = "Western Suburbs", Code = "ZONE_WEST" };
        context.Zones.AddRange(zoneA, zoneB);

        var colaba = new Area { Id = 1, Name = "Colaba", Code = "COLABA", ZoneId = 1 };
        var andheri = new Area { Id = 2, Name = "Andheri", Code = "ANDHERI", ZoneId = 2 };
        context.Areas.AddRange(colaba, andheri);

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
    public async Task Test1_Admin_CanCreateZone_Successfully()
    {
        var db = GetInMemoryDbContext(nameof(Test1_Admin_CanCreateZone_Successfully));
        var controller = new ZonesController(db);
        SetControllerUser(controller, 1, UserRole.Admin, "admin@delivery.com");

        var result = await controller.CreateZone(new CreateZoneRequest
        {
            Name = "Eastern Suburbs",
            Code = "ZONE_EAST"
        });

        var createdAtResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var zone = Assert.IsType<Zone>(createdAtResult.Value);
        Assert.Equal("Eastern Suburbs", zone.Name);
        Assert.Equal("ZONE_EAST", zone.Code);
        Assert.Equal(3, await db.Zones.CountAsync());
    }

    [Fact]
    public async Task Test2_Admin_CannotCreateDuplicateZoneCode_ReturnsConflict()
    {
        var db = GetInMemoryDbContext(nameof(Test2_Admin_CannotCreateDuplicateZoneCode_ReturnsConflict));
        var controller = new ZonesController(db);
        SetControllerUser(controller, 1, UserRole.Admin, "admin@delivery.com");

        var result = await controller.CreateZone(new CreateZoneRequest
        {
            Name = "Duplicate Zone",
            Code = "ZONE_SOUTH" // Existing code
        });

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task Test3_Admin_CanUpdateZone()
    {
        var db = GetInMemoryDbContext(nameof(Test3_Admin_CanUpdateZone));
        var controller = new ZonesController(db);
        SetControllerUser(controller, 1, UserRole.Admin, "admin@delivery.com");

        var result = await controller.UpdateZone(1, new UpdateZoneRequest
        {
            Name = "South Mumbai Island City",
            Code = "ZONE_SOUTH_UPDATED"
        });

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var zone = Assert.IsType<Zone>(okResult.Value);
        Assert.Equal("South Mumbai Island City", zone.Name);

        var dbZone = await db.Zones.FindAsync(1);
        Assert.Equal("South Mumbai Island City", dbZone!.Name);
    }

    [Fact]
    public async Task Test4_Admin_CanDeleteEmptyZone_AndRejectsNonEmptyZone()
    {
        var db = GetInMemoryDbContext(nameof(Test4_Admin_CanDeleteEmptyZone_AndRejectsNonEmptyZone));
        var controller = new ZonesController(db);
        SetControllerUser(controller, 1, UserRole.Admin, "admin@delivery.com");

        // Attempt to delete Zone 1 (contains Area 1: Colaba) -> Should reject
        var rejectResult = await controller.DeleteZone(1);
        Assert.IsType<BadRequestObjectResult>(rejectResult);

        // Create empty zone and delete -> Should succeed
        var newZone = new Zone { Id = 10, Name = "Empty Zone", Code = "ZONE_EMPTY" };
        db.Zones.Add(newZone);
        await db.SaveChangesAsync();

        var successResult = await controller.DeleteZone(10);
        Assert.IsType<NoContentResult>(successResult);
        Assert.Null(await db.Zones.FindAsync(10));
    }

    [Fact]
    public async Task Test5_Admin_CanCreateArea_AndReassignZone()
    {
        var db = GetInMemoryDbContext(nameof(Test5_Admin_CanCreateArea_AndReassignZone));
        var controller = new AreasController(db);
        SetControllerUser(controller, 1, UserRole.Admin, "admin@delivery.com");

        // Create Area in Zone 1
        var createResult = await controller.CreateArea(new CreateAreaRequest
        {
            Name = "Bandra West",
            Code = "BANDRA_W",
            ZoneId = 1
        });
        var created = Assert.IsType<Area>(Assert.IsType<CreatedAtActionResult>(createResult.Result).Value);
        Assert.Equal(1, created.ZoneId);

        // Reassign Area to Zone 2
        var updateResult = await controller.UpdateArea(created.Id, new UpdateAreaRequest
        {
            Name = "Bandra West",
            Code = "BANDRA_W",
            ZoneId = 2 // Reassign
        });
        var updated = Assert.IsType<Area>(Assert.IsType<OkObjectResult>(updateResult.Result).Value);
        Assert.Equal(2, updated.ZoneId);

        var dbArea = await db.Areas.FindAsync(created.Id);
        Assert.Equal(2, dbArea!.ZoneId);
    }

    [Fact]
    public async Task Test6_Admin_CanUpdateRateCard_AndPricingReflectsImmediately()
    {
        var db = GetInMemoryDbContext(nameof(Test6_Admin_CanUpdateRateCard_AndPricingReflectsImmediately));
        var rateController = new RateCardsController(db);
        SetControllerUser(rateController, 1, UserRole.Admin, "admin@delivery.com");

        var pricingService = new PricingService(db);

        // 1. Initial pricing check for B2C IntraZone 5kg COD -> Fee: 5 * 40 = 200, COD: 40, Total: 240
        var initialPrice = await pricingService.CalculatePriceAsync(new CalculatePriceRequest
        {
            PickupAreaId = 1, // Colaba (Zone 1)
            DropAreaId = 1,   // Colaba (Zone 1)
            Length = 10, Breadth = 10, Height = 10, ActualWeight = 5.0m,
            OrderType = OrderType.B2C, PaymentType = PaymentType.COD
        });
        Assert.Equal(200.00m, initialPrice.DeliveryFee);
        Assert.Equal(240.00m, initialPrice.TotalAmount);

        // 2. Admin updates B2C RateCard: IntraRate -> 50, COD -> 60
        var updateResult = await rateController.UpdateRateCard(1, new UpdateRateCardRequest
        {
            IntraZoneRatePerKg = 50.00m,
            InterZoneRatePerKg = 75.00m,
            CODSurcharge = 60.00m
        });
        Assert.IsType<OkObjectResult>(updateResult.Result);

        // 3. New pricing check -> Fee: 5 * 50 = 250, COD: 60, Total: 310
        var updatedPrice = await pricingService.CalculatePriceAsync(new CalculatePriceRequest
        {
            PickupAreaId = 1,
            DropAreaId = 1,
            Length = 10, Breadth = 10, Height = 10, ActualWeight = 5.0m,
            OrderType = OrderType.B2C, PaymentType = PaymentType.COD
        });
        Assert.Equal(50.00m, updatedPrice.RatePerKg);
        Assert.Equal(250.00m, updatedPrice.DeliveryFee);
        Assert.Equal(60.00m, updatedPrice.CODSurcharge);
        Assert.Equal(310.00m, updatedPrice.TotalAmount);
    }
}
