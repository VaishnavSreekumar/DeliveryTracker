using System.ComponentModel.DataAnnotations;
using DeliveryTracker.API.Enums;

namespace DeliveryTracker.API.DTOs;

public class UpdateOrderStatusRequest
{
    [Required]
    public OrderStatus Status { get; set; }

    [Required]
    public int ActorId { get; set; }

    public string? Notes { get; set; }
}
