using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DeliveryTracker.API.Data;
using DeliveryTracker.API.DTOs;
using DeliveryTracker.API.Entities;

namespace DeliveryTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AreasController : ControllerBase
{
    private readonly AppDbContext _context;

    public AreasController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Retrieves all serviced delivery areas with their parent zones.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Area>>> GetAreas()
    {
        return await _context.Areas
            .Include(a => a.Zone)
            .OrderBy(a => a.ZoneId)
            .ThenBy(a => a.Name)
            .ToListAsync();
    }

    /// <summary>
    /// Retrieves a specific area by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Area>> GetArea(int id)
    {
        var area = await _context.Areas
            .Include(a => a.Zone)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (area == null)
        {
            return NotFound(new { message = $"Area with ID {id} not found." });
        }

        return Ok(area);
    }

    /// <summary>
    /// Creates a new delivery area assigned to a zone (Admin only).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Area>> CreateArea([FromBody] CreateAreaRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var zone = await _context.Zones.FindAsync(request.ZoneId);
        if (zone == null)
        {
            return BadRequest(new { message = $"Target Zone with ID {request.ZoneId} does not exist." });
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        if (await _context.Areas.AnyAsync(a => a.Code.ToUpper() == normalizedCode))
        {
            return Conflict(new { message = $"Area with code '{normalizedCode}' already exists." });
        }

        var area = new Area
        {
            Name = request.Name.Trim(),
            Code = normalizedCode,
            ZoneId = request.ZoneId
        };

        _context.Areas.Add(area);
        await _context.SaveChangesAsync();

        area.Zone = zone;
        return CreatedAtAction(nameof(GetArea), new { id = area.Id }, area);
    }

    /// <summary>
    /// Updates an existing delivery area or reassigns it to another zone (Admin only).
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<Area>> UpdateArea(int id, [FromBody] UpdateAreaRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var area = await _context.Areas.Include(a => a.Zone).FirstOrDefaultAsync(a => a.Id == id);
        if (area == null)
        {
            return NotFound(new { message = $"Area with ID {id} not found." });
        }

        var zone = await _context.Zones.FindAsync(request.ZoneId);
        if (zone == null)
        {
            return BadRequest(new { message = $"Target Zone with ID {request.ZoneId} does not exist." });
        }

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        if (await _context.Areas.AnyAsync(a => a.Id != id && a.Code.ToUpper() == normalizedCode))
        {
            return Conflict(new { message = $"Another area with code '{normalizedCode}' already exists." });
        }

        area.Name = request.Name.Trim();
        area.Code = normalizedCode;
        area.ZoneId = request.ZoneId;

        await _context.SaveChangesAsync();
        area.Zone = zone;

        return Ok(area);
    }

    /// <summary>
    /// Deletes a delivery area (Admin only).
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteArea(int id)
    {
        var area = await _context.Areas.FindAsync(id);
        if (area == null)
        {
            return NotFound(new { message = $"Area with ID {id} not found." });
        }

        // Check if referenced by existing orders
        var hasOrders = await _context.Orders.AnyAsync(o => o.PickupAreaId == id || o.DropAreaId == id);
        if (hasOrders)
        {
            return BadRequest(new { message = $"Cannot delete area '{area.Name}' because it is referenced in existing shipment orders." });
        }

        _context.Areas.Remove(area);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
