using DeliveryTracker.API.DTOs;

namespace DeliveryTracker.API.Services;

public interface INotificationService
{
    Task NotifyCustomerAsync(
        int userId,
        int orderId,
        string trackingNumber,
        string eventType,
        string title,
        string message,
        string? recipientEmail = null,
        string? recipientPhone = null);

    Task<IEnumerable<NotificationDto>> GetCustomerNotificationsAsync(int userId);
    Task<bool> MarkNotificationAsReadAsync(int notificationId, int userId);
    Task<IEnumerable<NotificationDto>> GetOrderCommunicationLogsAsync(int orderId);
}
