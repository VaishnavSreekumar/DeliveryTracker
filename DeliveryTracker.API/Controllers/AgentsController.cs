using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DeliveryTracker.API.Data;
using DeliveryTracker.API.DTOs;

namespace DeliveryTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentsController : ControllerBase
{
    private readonly AppDbContext _context;

    public AgentsController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Retrieves all delivery agents with their zone and availability status (Admin only).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<AdminAgentDto>>> GetAgents()
    {
        var agents = await _context.Agents
            .Include(a => a.User)
            .Include(a => a.Zone)
            .OrderBy(a => a.Id)
            .ToListAsync();

        return Ok(agents.Select(a => new AdminAgentDto
        {
            Id = a.Id,
            UserId = a.UserId,
            Name = a.User?.FullName ?? $"Agent {a.Id}",
            Email = a.User?.Email ?? string.Empty,
            ZoneId = a.ZoneId,
            ZoneName = a.Zone?.Name ?? "Unknown Zone",
            IsAvailable = a.IsAvailable,
            Latitude = a.Latitude,
            Longitude = a.Longitude
        }));
    }
}
