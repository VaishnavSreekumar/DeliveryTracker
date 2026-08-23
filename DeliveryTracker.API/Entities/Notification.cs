using DeliveryTracker.API.Enums;

namespace DeliveryTracker.API.Entities;

public class Notification
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string RecipientEmail { get; set; } = string.Empty;
    public string? RecipientPhone { get; set; }
    public bool IsRead { get; set; } = false;

    public CommunicationChannel Channel { get; set; } = CommunicationChannel.InApp;
    public string EventType { get; set; } = "General";
    public CommunicationStatus DeliveryStatus { get; set; } = CommunicationStatus.Sent;
    public string? ErrorMessage { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
