using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DeliveryTracker.API.Data;
using DeliveryTracker.API.DTOs;
using DeliveryTracker.API.Entities;

namespace DeliveryTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ZonesController : ControllerBase
{
    private readonly AppDbContext _context;

    public ZonesController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Retrieves all delivery zones and their associated serviced areas.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Zone>>> GetZones()
    {
        return await _context.Zones
            .Include(z => z.Areas)
            .OrderBy(z => z.Id)
            .ToListAsync();
    }

    /// <summary>
    /// Retrieves a specific delivery zone by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Zone>> GetZone(int id)
    {
        var zone = await _context.Zones
            .Include(z => z.Areas)
            .FirstOrDefaultAsync(z => z.Id == id);

        if (zone == null)
        {
            return NotFound(new { message = $"Zone with ID {id} not found." });
        }

        return Ok(zone);
    }

    /// <summary>
    /// Creates a new delivery zone (Admin only).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Zone>> CreateZone([FromBody] CreateZoneRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        if (await _context.Zones.AnyAsync(z => z.Code.ToUpper() == normalizedCode))
        {
            return Conflict(new { message = $"Zone with code '{normalizedCode}' already exists." });
        }

        var zone = new Zone
        {
            Name = request.Name.Trim(),
            Code = normalizedCode
        };

        _context.Zones.Add(zone);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetZone), new { id = zone.Id }, zone);
    }

    /// <summary>
    /// Updates an existing delivery zone (Admin only).
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Zone>> UpdateZone(int id, [FromBody] UpdateZoneRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var zone = await _context.Zones.Include(z => z.Areas).FirstOrDefaultAsync(z => z.Id == id);
        if (zone == null)
        {
            return NotFound(new { message = $"Zone with ID {id} not found." });
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        if (await _context.Zones.AnyAsync(z => z.Id != id && z.Code.ToUpper() == normalizedCode))
        {
            return Conflict(new { message = $"Another zone with code '{normalizedCode}' already exists." });
        }

        zone.Name = request.Name.Trim();
        zone.Code = normalizedCode;

        await _context.SaveChangesAsync();
        return Ok(zone);
    }

    /// <summary>
    /// Deletes a delivery zone (Admin only). Rejects if active areas or agents are assigned to it.
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteZone(int id)
    {
        var zone = await _context.Zones
            .Include(z => z.Areas)
            .Include(z => z.Agents)
            .FirstOrDefaultAsync(z => z.Id == id);

        if (zone == null)
        {
            return NotFound(new { message = $"Zone with ID {id} not found." });
        }

        if (zone.Areas.Any())
        {
            return BadRequest(new { message = $"Cannot delete zone '{zone.Name}' because it contains {zone.Areas.Count} assigned areas. Reassign or delete the areas first." });
        }

        if (zone.Agents.Any())
        {
            return BadRequest(new { message = $"Cannot delete zone '{zone.Name}' because {zone.Agents.Count} agents are stationed in it." });
        }

        _context.Zones.Remove(zone);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
