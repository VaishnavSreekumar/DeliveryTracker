using System.ComponentModel.DataAnnotations;

namespace DeliveryTracker.API.DTOs;

public class RescheduleOrderRequest
{
    [Required]
    public int CustomerId { get; set; }

    [Required]
    public DateTime RescheduledDate { get; set; }

    public string? Notes { get; set; }
}
