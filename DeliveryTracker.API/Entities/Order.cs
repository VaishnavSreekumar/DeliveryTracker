using DeliveryTracker.API.Enums;

namespace DeliveryTracker.API.Entities;

public class Order
{
    public int Id { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;

    public int CustomerId { get; set; }
    public User? Customer { get; set; }

    public int PickupAreaId { get; set; }
    public Area? PickupArea { get; set; }

    public int DropAreaId { get; set; }
    public Area? DropArea { get; set; }

    public string PickupAddress { get; set; } = string.Empty;
    public string DropAddress { get; set; } = string.Empty;

    public int? AssignedAgentId { get; set; }
    public Agent? AssignedAgent { get; set; }

    public OrderType OrderType { get; set; }
    public PaymentType PaymentType { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Created;

    // Dimensions (cm) & Actual Weight (kg)
    public double LengthCm { get; set; }
    public double WidthCm { get; set; }
    public double HeightCm { get; set; }
    public decimal ActualWeightKg { get; set; }

    // Calculated Weights (kg)
    public decimal VolumetricWeightKg { get; set; }
    public decimal ChargeableWeightKg { get; set; }

    // Pricing Breakdown
    public decimal RatePerKg { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal CODSurcharge { get; set; }
    public decimal TotalAmount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = new List<OrderStatusHistory>();
    public ICollection<DeliveryAttempt> DeliveryAttempts { get; set; } = new List<DeliveryAttempt>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
