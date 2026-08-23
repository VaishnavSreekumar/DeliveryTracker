using DeliveryTracker.API.Enums;

namespace DeliveryTracker.API.Entities;

public class RateCard
{
    public int Id { get; set; }
    public OrderType OrderType { get; set; }
    public decimal IntraZoneRatePerKg { get; set; }
    public decimal InterZoneRatePerKg { get; set; }
    public decimal CODSurcharge { get; set; }
}
