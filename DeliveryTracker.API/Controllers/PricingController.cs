using Microsoft.AspNetCore.Mvc;
using DeliveryTracker.API.DTOs;
using DeliveryTracker.API.Services;

namespace DeliveryTracker.API.Controllers;

[ApiController]
[Route("api/orders")]
public class PricingController : ControllerBase
{
    private readonly IPricingService _pricingService;

    public PricingController(IPricingService pricingService)
    {
        _pricingService = pricingService;
    }

    [HttpPost("calculate-price")]
    public async Task<ActionResult<PriceCalculationResult>> CalculatePrice([FromBody] CalculatePriceRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _pricingService.CalculatePriceAsync(request);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(500, new { message = ex.Message });
        }
    }
}
