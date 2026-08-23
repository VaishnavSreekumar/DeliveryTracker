namespace DeliveryTracker.API.DTOs;

public class NotificationDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int OrderId { get; set; }
    public string OrderTrackingNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public string? RecipientPhone { get; set; }
    public bool IsRead { get; set; }
    public string Channel { get; set; } = "InApp";
    public string EventType { get; set; } = "General";
    public string DeliveryStatus { get; set; } = "Sent";
    public string? ErrorMessage { get; set; }
    public DateTime SentAt { get; set; }
}
