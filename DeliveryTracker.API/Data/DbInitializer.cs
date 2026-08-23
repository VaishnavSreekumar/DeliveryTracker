using Microsoft.EntityFrameworkCore;
using DeliveryTracker.API.Entities;
using DeliveryTracker.API.Enums;

namespace DeliveryTracker.API.Data;

public static class DbInitializer
{
    public static void Initialize(AppDbContext context)
    {
        // 1. Apply migrations automatically (Adjustment #2)
        context.Database.Migrate();

        // 2. Seed Zones & Areas
        if (!context.Zones.Any())
        {
            var zoneA = new Zone { Name = "Zone A", Code = "ZONE_A" };
            var zoneB = new Zone { Name = "Zone B", Code = "ZONE_B" };
            var zoneC = new Zone { Name = "Zone C", Code = "ZONE_C" };

            context.Zones.AddRange(zoneA, zoneB, zoneC);
            context.SaveChanges();

            var areas = new List<Area>
            {
                new Area { Name = "Colaba", Code = "COLABA", ZoneId = zoneA.Id },
                new Area { Name = "Dadar", Code = "DADAR", ZoneId = zoneA.Id },
                new Area { Name = "Andheri", Code = "ANDHERI", ZoneId = zoneB.Id },
                new Area { Name = "Bandra", Code = "BANDRA", ZoneId = zoneB.Id },
                new Area { Name = "Thane", Code = "THANE", ZoneId = zoneC.Id },
                new Area { Name = "Powai", Code = "POWAI", ZoneId = zoneC.Id }
            };

            context.Areas.AddRange(areas);
            context.SaveChanges();
        }

        // 3. Seed Rate Cards
        if (!context.RateCards.Any())
        {
            var rateCards = new List<RateCard>
            {
                new RateCard
                {
                    OrderType = OrderType.B2C,
                    IntraZoneRatePerKg = 40.00m,
                    InterZoneRatePerKg = 60.00m,
                    CODSurcharge = 40.00m
                },
                new RateCard
                {
                    OrderType = OrderType.B2B,
                    IntraZoneRatePerKg = 30.00m,
                    InterZoneRatePerKg = 50.00m,
                    CODSurcharge = 30.00m
                }
            };

            context.RateCards.AddRange(rateCards);
            context.SaveChanges();
        }

        // 4. Seed Users & Agents
        if (!context.Users.Any())
        {
            var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<User>();

            var adminUser = new User
            {
                FullName = "System Admin",
                Email = "admin@delivery.com",
                PhoneNumber = "+18005550100",
                Role = UserRole.Admin,
                CreatedAt = DateTime.UtcNow
            };
            adminUser.PasswordHash = hasher.HashPassword(adminUser, "Admin@123");

            var customerUser = new User
            {
                FullName = "John Customer",
                Email = "customer@delivery.com",
                PhoneNumber = "+919876543210",
                Role = UserRole.Customer,
                CreatedAt = DateTime.UtcNow
            };
            customerUser.PasswordHash = hasher.HashPassword(customerUser, "Customer@123");

            var agent1User = new User
            {
                FullName = "Raj Agent",
                Email = "agent1@delivery.com",
                PhoneNumber = "+919876543211",
                Role = UserRole.Agent,
                CreatedAt = DateTime.UtcNow
            };
            agent1User.PasswordHash = hasher.HashPassword(agent1User, "Agent@123");

            var agent2User = new User
            {
                FullName = "Vikram Agent",
                Email = "agent2@delivery.com",
                PhoneNumber = "+919876543212",
                Role = UserRole.Agent,
                CreatedAt = DateTime.UtcNow
            };
            agent2User.PasswordHash = hasher.HashPassword(agent2User, "Agent@123");

            context.Users.AddRange(adminUser, customerUser, agent1User, agent2User);
            context.SaveChanges();

            // Fetch Zone A and Zone B IDs
            var zoneA = context.Zones.First(z => z.Code == "ZONE_A");
            var zoneB = context.Zones.First(z => z.Code == "ZONE_B");

            var agent1 = new Agent
            {
                UserId = agent1User.Id,
                ZoneId = zoneA.Id,
                IsAvailable = true,
                Latitude = 18.9220, // Colaba coordinates
                Longitude = 72.8347
            };

            var agent2 = new Agent
            {
                UserId = agent2User.Id,
                ZoneId = zoneB.Id,
                IsAvailable = true,
                Latitude = 19.1197, // Andheri coordinates
                Longitude = 72.8464
            };

            context.Agents.AddRange(agent1, agent2);
            context.SaveChanges();
        }
    }
}
