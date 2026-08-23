namespace DeliveryTracker.API.DTOs;

public class PriceCalculationResult
{
    public string PickupArea { get; set; } = string.Empty;
    public string PickupZone { get; set; } = string.Empty;
    public string DropArea { get; set; } = string.Empty;
    public string DropZone { get; set; } = string.Empty;
    public bool IsIntraZone { get; set; }

    public decimal ActualWeight { get; set; }
    public decimal VolumetricWeight { get; set; }
    public decimal ChargeableWeight { get; set; }

    public decimal RatePerKg { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal CODSurcharge { get; set; }
    public decimal TotalAmount { get; set; }
}
