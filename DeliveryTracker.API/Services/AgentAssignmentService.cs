using Microsoft.EntityFrameworkCore;
using DeliveryTracker.API.Data;
using DeliveryTracker.API.DTOs;
using DeliveryTracker.API.Entities;

namespace DeliveryTracker.API.Services;

public class AgentAssignmentService : IAgentAssignmentService
{
    private readonly AppDbContext _context;

    public AgentAssignmentService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AgentAssignmentResponse> AutoAssignAgentAsync(int orderId, int? excludeAgentId = null)
    {
        // STEP 1: Load Order
        var order = await _context.Orders
            .Include(o => o.PickupArea!).ThenInclude(a => a.Zone)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
        {
            throw new KeyNotFoundException($"Order with ID {orderId} not found.");
        }

        // STEP 2: Check if Order is already assigned
        if (order.AssignedAgentId.HasValue)
        {
            throw new InvalidOperationException($"Order '{order.TrackingNumber}' is already assigned to agent ID {order.AssignedAgentId}.");
        }

        // STEP 3: Load Pickup Area & Zone
        var pickupArea = order.PickupArea;
        if (pickupArea == null)
        {
            throw new InvalidOperationException($"Order '{order.TrackingNumber}' does not have a valid pickup area.");
        }

        int pickupZoneId = pickupArea.ZoneId;
        (double pickupLat, double pickupLon) = GetZoneCoordinates(pickupArea.Zone?.Code ?? "");

        // STEP 4: Find all available agents (IsAvailable == true), excluding excludeAgentId if specified
        var query = _context.Agents
            .Include(a => a.User)
            .Include(a => a.Zone)
            .Where(a => a.IsAvailable);

        if (excludeAgentId.HasValue)
        {
            query = query.Where(a => a.Id != excludeAgentId.Value);
        }

        var availableAgents = await query.ToListAsync();

        // STEP 5: If no available agents exist, reject gracefully
        if (!availableAgents.Any())
        {
            throw new InvalidOperationException("No available delivery agents found.");
        }

        // STEP 6 & 7: Calculate Haversine distance and prioritize (1. Same pickup zone, 2. Shortest distance)
        var rankedAgents = availableAgents
            .Select(agent =>
            {
                bool isSameZone = (agent.ZoneId == pickupZoneId);
                double distanceKm = CalculateHaversineDistanceKm(agent.Latitude, agent.Longitude, pickupLat, pickupLon);
                return new
                {
                    Agent = agent,
                    IsSameZone = isSameZone,
                    DistanceKm = Math.Round(distanceKm, 2)
                };
            })
            .OrderByDescending(x => x.IsSameZone) // Same zone preferred (true > false)
            .ThenBy(x => x.DistanceKm)            // Shortest distance secondary
            .ToList();

        var selected = rankedAgents.First();
        var selectedAgent = selected.Agent;

        // STEP 8, 9 & 10: Atomic assignment & availability status update
        order.AssignedAgentId = selectedAgent.Id;
        order.UpdatedAt = DateTime.UtcNow;
        selectedAgent.IsAvailable = false;

        await _context.SaveChangesAsync();

        return new AgentAssignmentResponse
        {
            OrderId = order.Id,
            TrackingNumber = order.TrackingNumber,
            AssignedAgent = new AssignedAgentDto
            {
                Id = selectedAgent.Id,
                Name = selectedAgent.User?.FullName ?? "Agent",
                Email = selectedAgent.User?.Email ?? string.Empty,
                ZoneName = selectedAgent.Zone?.Name ?? "Unknown Zone",
                DistanceKm = selected.DistanceKm
            },
            Message = "Agent assigned successfully."
        };
    }

    /// <summary>
    /// Returns representative simulated coordinates for a zone based on code.
    /// </summary>
    private static (double Lat, double Lon) GetZoneCoordinates(string zoneCode)
    {
        return zoneCode switch
        {
            "ZONE_A" => (18.9220, 72.8347), // Colaba / South Mumbai
            "ZONE_B" => (19.1197, 72.8464), // Andheri / Bandra / Western Suburbs
            "ZONE_C" => (19.2183, 72.9781), // Thane / Powai / Central Suburbs
            _ => (18.9220, 72.8347)
        };
    }

    /// <summary>
    /// Standard Haversine distance calculation in kilometers.
    /// </summary>
    private static double CalculateHaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double EarthRadiusKm = 6371.0;

        double dLat = ToRadians(lat2 - lat1);
        double dLon = ToRadians(lon2 - lon1);

        double a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                   Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                   Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);

        double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return EarthRadiusKm * c;
    }

    private static double ToRadians(double angle) => (Math.PI / 180.0) * angle;
}
