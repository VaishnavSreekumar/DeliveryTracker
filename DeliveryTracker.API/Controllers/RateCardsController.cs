using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DeliveryTracker.API.Data;
using DeliveryTracker.API.DTOs;
using DeliveryTracker.API.Entities;

namespace DeliveryTracker.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RateCardsController : ControllerBase
{
    private readonly AppDbContext _context;

    public RateCardsController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Retrieves all configured rate cards (B2C, B2B).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RateCard>>> GetRateCards()
    {
        return await _context.RateCards
            .OrderBy(r => r.Id)
            .ToListAsync();
    }

    /// <summary>
    /// Retrieves a specific rate card by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<RateCard>> GetRateCard(int id)
    {
        var rateCard = await _context.RateCards.FindAsync(id);
        if (rateCard == null)
        {
            return NotFound(new { message = $"RateCard with ID {id} not found." });
        }

        return Ok(rateCard);
    }

    /// <summary>
    /// Updates pricing configuration on a rate card (Admin only).
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RateCard>> UpdateRateCard(int id, [FromBody] UpdateRateCardRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var rateCard = await _context.RateCards.FindAsync(id);
        if (rateCard == null)
        {
            return NotFound(new { message = $"RateCard with ID {id} not found." });
        }

        rateCard.IntraZoneRatePerKg = request.IntraZoneRatePerKg;
        rateCard.InterZoneRatePerKg = request.InterZoneRatePerKg;
        rateCard.CODSurcharge = request.CODSurcharge;

        await _context.SaveChangesAsync();
        return Ok(rateCard);
    }
}
