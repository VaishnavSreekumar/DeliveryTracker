using System.ComponentModel.DataAnnotations;
using DeliveryTracker.API.Enums;

namespace DeliveryTracker.API.DTOs;

public class CreateZoneRequest
{
    [Required(ErrorMessage = "Zone name is required.")]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Zone code is required.")]
    [StringLength(20, MinimumLength = 2)]
    public string Code { get; set; } = string.Empty;
}

public class UpdateZoneRequest
{
    [Required(ErrorMessage = "Zone name is required.")]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Zone code is required.")]
    [StringLength(20, MinimumLength = 2)]
    public string Code { get; set; } = string.Empty;
}

public class CreateAreaRequest
{
    [Required(ErrorMessage = "Area name is required.")]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Area code is required.")]
    [StringLength(20, MinimumLength = 2)]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "ZoneId is required.")]
    public int ZoneId { get; set; }
}

public class UpdateAreaRequest
{
    [Required(ErrorMessage = "Area name is required.")]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Area code is required.")]
    [StringLength(20, MinimumLength = 2)]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "ZoneId is required.")]
    public int ZoneId { get; set; }
}

public class UpdateRateCardRequest
{
    [Range(0.01, 10000.0, ErrorMessage = "IntraZoneRatePerKg must be greater than 0.")]
    public decimal IntraZoneRatePerKg { get; set; }

    [Range(0.01, 10000.0, ErrorMessage = "InterZoneRatePerKg must be greater than 0.")]
    public decimal InterZoneRatePerKg { get; set; }

    [Range(0.0, 5000.0, ErrorMessage = "CODSurcharge must be 0 or greater.")]
    public decimal CODSurcharge { get; set; }
}
