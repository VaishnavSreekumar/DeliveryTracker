using System.ComponentModel.DataAnnotations;

namespace DeliveryTracker.API.DTOs;

public class RegisterRequest
{
    [Required]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required.")]
    [RegularExpression(@"^\+?[1-9]\d{7,14}$", ErrorMessage = "Phone number must be a valid international number in E.164 format (e.g. +919037350803).")]
    public string PhoneNumber { get; set; } = string.Empty;
}
