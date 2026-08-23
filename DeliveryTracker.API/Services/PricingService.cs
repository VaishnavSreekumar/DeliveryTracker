using Microsoft.EntityFrameworkCore;
using DeliveryTracker.API.Data;
using DeliveryTracker.API.DTOs;
using DeliveryTracker.API.Enums;

namespace DeliveryTracker.API.Services;

public class PricingService : IPricingService
{
    private readonly AppDbContext _context;

    public PricingService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PriceCalculationResult> CalculatePriceAsync(CalculatePriceRequest request)
    {
        // 1. Basic Dimension & Weight Validation
        if (request.Length <= 0 || request.Breadth <= 0 || request.Height <= 0)
        {
            throw new ArgumentException("Package dimensions (length, breadth, height) must be greater than 0.");
        }

        if (request.ActualWeight <= 0)
        {
            throw new ArgumentException("Actual weight must be greater than 0.");
        }

        // 2. Fetch Pickup & Drop Areas with their respective Zones
        var pickupArea = await _context.Areas
            .Include(a => a.Zone)
            .FirstOrDefaultAsync(a => a.Id == request.PickupAreaId);

        if (pickupArea == null)
        {
            throw new KeyNotFoundException($"Pickup area with ID {request.PickupAreaId} not found.");
        }

        var dropArea = await _context.Areas
            .Include(a => a.Zone)
            .FirstOrDefaultAsync(a => a.Id == request.DropAreaId);

        if (dropArea == null)
        {
            throw new KeyNotFoundException($"Drop area with ID {request.DropAreaId} not found.");
        }

        // 3. Fetch RateCard dynamically from Database
        var rateCard = await _context.RateCards
            .FirstOrDefaultAsync(r => r.OrderType == request.OrderType);

        if (rateCard == null)
        {
            throw new InvalidOperationException($"RateCard for OrderType '{request.OrderType}' is not configured in database.");
        }

        // 4. Calculate Volumetric Weight (Length x Breadth x Height / 5000)
        decimal volumetricWeight = (decimal)((request.Length * request.Breadth * request.Height) / 5000.0);

        // 5. Calculate Chargeable Weight MAX(ActualWeight, VolumetricWeight)
        decimal chargeableWeight = Math.Max(request.ActualWeight, volumetricWeight);

        // 6. Determine Zone Relationship (IntraZone vs InterZone)
        bool isIntraZone = (pickupArea.ZoneId == dropArea.ZoneId);
        decimal ratePerKg = isIntraZone ? rateCard.IntraZoneRatePerKg : rateCard.InterZoneRatePerKg;

        // 7. Calculate Delivery Fee
        decimal deliveryFee = chargeableWeight * ratePerKg;

        // 8. Calculate COD Surcharge
        decimal codSurcharge = (request.PaymentType == PaymentType.COD) ? rateCard.CODSurcharge : 0.00m;

        // 9. Calculate Total Amount
        decimal totalAmount = deliveryFee + codSurcharge;

        return new PriceCalculationResult
        {
            PickupArea = pickupArea.Name,
            PickupZone = pickupArea.Zone?.Name ?? "Unknown Zone",
            DropArea = dropArea.Name,
            DropZone = dropArea.Zone?.Name ?? "Unknown Zone",
            IsIntraZone = isIntraZone,
            ActualWeight = request.ActualWeight,
            VolumetricWeight = Math.Round(volumetricWeight, 2),
            ChargeableWeight = Math.Round(chargeableWeight, 2),
            RatePerKg = ratePerKg,
            DeliveryFee = Math.Round(deliveryFee, 2),
            CODSurcharge = Math.Round(codSurcharge, 2),
            TotalAmount = Math.Round(totalAmount, 2)
        };
    }
}
