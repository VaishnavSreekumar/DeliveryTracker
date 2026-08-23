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
    public bool IsRead { get; set; }
    public DateTime SentAt { get; set; }
}
