using System.ComponentModel.DataAnnotations;
using DeliveryTracker.API.Enums;

namespace DeliveryTracker.API.DTOs;

public class ManualAssignAgentRequest
{
    [Required(ErrorMessage = "Agent ID is required.")]
    public int AgentId { get; set; }
}

public class AdminOverrideStatusRequest
{
    [Required(ErrorMessage = "Target status is required.")]
    public OrderStatus Status { get; set; }

    [Required(ErrorMessage = "Override reason is mandatory.")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "Override reason must be between 3 and 500 characters.")]
    public string Reason { get; set; } = string.Empty;
}

public class AdminCustomerDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class AdminAgentDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int ZoneId { get; set; }
    public string ZoneName { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
