using DeliveryTracker.API.Enums;

namespace DeliveryTracker.API.DTOs;

public class OrderResponse
{
    public int Id { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;

    public string PickupArea { get; set; } = string.Empty;
    public string PickupZone { get; set; } = string.Empty;
    public string DropArea { get; set; } = string.Empty;
    public string DropZone { get; set; } = string.Empty;

    public string PickupAddress { get; set; } = string.Empty;
    public string DropAddress { get; set; } = string.Empty;

    public double LengthCm { get; set; }
    public double WidthCm { get; set; }
    public double HeightCm { get; set; }

    public decimal ActualWeight { get; set; }
    public decimal VolumetricWeight { get; set; }
    public decimal ChargeableWeight { get; set; }

    public OrderType OrderType { get; set; }
    public PaymentType PaymentType { get; set; }

    public decimal RatePerKg { get; set; }
    public decimal DeliveryFee { get; set; }
    public decimal CODSurcharge { get; set; }
    public decimal TotalAmount { get; set; }

    public OrderStatus Status { get; set; }
    public int? AssignedAgentId { get; set; }
    public string? AssignedAgentName { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<OrderStatusHistoryDto> StatusHistory { get; set; } = new();
}
