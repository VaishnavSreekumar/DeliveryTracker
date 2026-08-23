using System.ComponentModel.DataAnnotations;
using DeliveryTracker.API.Enums;

namespace DeliveryTracker.API.DTOs;

public class CreateOrderRequest
{
    [Required]
    public int CustomerId { get; set; }

    [Required]
    public int PickupAreaId { get; set; }

    [Required]
    public int DropAreaId { get; set; }

    public string PickupAddress { get; set; } = string.Empty;
    public string DropAddress { get; set; } = string.Empty;

    [Range(0.01, double.MaxValue, ErrorMessage = "Length must be greater than 0.")]
    public double Length { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Breadth must be greater than 0.")]
    public double Breadth { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Height must be greater than 0.")]
    public double Height { get; set; }

    [Range(0.01, 10000.0, ErrorMessage = "ActualWeight must be greater than 0.")]
    public decimal ActualWeight { get; set; }

    [Required]
    public OrderType OrderType { get; set; }

    [Required]
    public PaymentType PaymentType { get; set; }
}
