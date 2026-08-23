using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DeliveryTracker.API.Data;
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

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Zone>>> GetZones()
    {
        return await _context.Zones
            .Include(z => z.Areas)
            .ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Zone>> GetZone(int id)
    {
        var zone = await _context.Zones
            .Include(z => z.Areas)
            .FirstOrDefaultAsync(z => z.Id == id);

        if (zone == null)
        {
            return NotFound(new { message = $"Zone with ID {id} not found." });
        }

        return zone;
    }
}
