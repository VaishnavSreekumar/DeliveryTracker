using Microsoft.EntityFrameworkCore;
using DeliveryTracker.API.Data;
using DeliveryTracker.API.DTOs;
using DeliveryTracker.API.Entities;
using DeliveryTracker.API.Enums;
using DeliveryTracker.API.Services.Communication;

namespace DeliveryTracker.API.Services;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _context;
    private readonly IEmailNotificationProvider _emailProvider;
    private readonly ISmsNotificationProvider _smsProvider;

    public NotificationService(
        AppDbContext context,
        IEmailNotificationProvider emailProvider,
        ISmsNotificationProvider smsProvider)
    {
        _context = context;
        _emailProvider = emailProvider;
        _smsProvider = smsProvider;
    }

    public async Task NotifyCustomerAsync(
        int userId,
        int orderId,
        string trackingNumber,
        string eventType,
        string title,
        string message,
        string? recipientEmail = null,
        string? recipientPhone = null)
    {
        var now = DateTime.UtcNow;

        // Resolve Email & Phone from authenticated user profile
        var user = await _context.Users.FindAsync(userId);
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            recipientEmail = user?.Email ?? "customer@delivery.com";
        }
        if (string.IsNullOrWhiteSpace(recipientPhone))
        {
            recipientPhone = user?.PhoneNumber ?? "+91 98765 43210";
        }

        var notificationsToSave = new List<Notification>();

        // 1. IN-APP Notification Channel (Only if not already created within 10s)
        bool hasInApp = await _context.Notifications
            .AnyAsync(n => n.OrderId == orderId && n.Channel == CommunicationChannel.InApp && n.EventType == eventType && n.SentAt >= now.AddSeconds(-10));

        if (!hasInApp)
        {
            var inAppNotification = new Notification
            {
                UserId = userId,
                OrderId = orderId,
                Title = title,
                Message = message,
                RecipientEmail = recipientEmail,
                RecipientPhone = recipientPhone,
                IsRead = false,
                Channel = CommunicationChannel.InApp,
                EventType = eventType,
                DeliveryStatus = CommunicationStatus.Sent,
                SentAt = now
            };
            notificationsToSave.Add(inAppNotification);
        }

        // 2. EMAIL Notification Channel (Failure-safe isolation)
        CommunicationResult emailResult;
        try
        {
            string emailSubject = $"DeliveryTracker — Order {trackingNumber} ({title})";
            string emailBody = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 8px;'>
                    <h2 style='color: #2563eb; margin-top: 0;'>DeliveryTracker Update</h2>
                    <p>Dear <strong>{user?.FullName ?? "Customer"}</strong>,</p>
                    <div style='background-color: #f8fafc; padding: 15px; border-radius: 6px; margin: 15px 0;'>
                        <p style='margin: 5px 0;'><strong>Tracking Number:</strong> {trackingNumber}</p>
                        <p style='margin: 5px 0;'><strong>Status:</strong> {title}</p>
                        <p style='margin: 5px 0;'><strong>Update:</strong> {message}</p>
                        <p style='margin: 5px 0; color: #64748b; font-size: 12px;'><strong>Timestamp:</strong> {now:yyyy-MM-dd HH:mm:ss} UTC</p>
                    </div>
                    <p>Track your shipment in real time on the <a href='http://localhost:5173' style='color: #2563eb; font-weight: bold;'>DeliveryTracker Portal</a>.</p>
                </div>";

            emailResult = await _emailProvider.SendEmailAsync(recipientEmail, emailSubject, emailBody, eventType, orderId);
        }
        catch (Exception ex)
        {
            emailResult = CommunicationResult.Fail("EmailProvider", ex.Message);
        }

        var emailDeliveryStatus = !emailResult.Success
            ? CommunicationStatus.Failed
            : (emailResult.Provider.Contains("Simulated") || emailResult.Provider.Contains("Development")
                ? CommunicationStatus.Simulated
                : CommunicationStatus.Sent);

        var emailLog = new Notification
        {
            UserId = userId,
            OrderId = orderId,
            Title = title,
            Message = message,
            RecipientEmail = recipientEmail,
            RecipientPhone = recipientPhone,
            IsRead = true,
            Channel = CommunicationChannel.Email,
            EventType = eventType,
            DeliveryStatus = emailDeliveryStatus,
            ErrorMessage = emailResult.ErrorMessage,
            SentAt = now
        };
        notificationsToSave.Add(emailLog);

        // 3. SMS Notification Channel (Dispatched on every status change event)
        if (ShouldSendSmsForEvent(eventType))
        {
            CommunicationResult smsResult;
            try
            {
                string smsMessage = FormatSmsMessage(trackingNumber, eventType, message);
                smsResult = await _smsProvider.SendSmsAsync(recipientPhone, smsMessage, eventType, orderId);
            }
            catch (Exception ex)
            {
                smsResult = CommunicationResult.Fail("SmsProvider", ex.Message);
            }

            var smsDeliveryStatus = !smsResult.Success
                ? CommunicationStatus.Failed
                : (smsResult.Provider.Contains("Simulated") || smsResult.Provider.Contains("Development")
                    ? CommunicationStatus.Simulated
                    : CommunicationStatus.Sent);

            var smsLog = new Notification
            {
                UserId = userId,
                OrderId = orderId,
                Title = title,
                Message = message,
                RecipientEmail = recipientEmail,
                RecipientPhone = recipientPhone,
                IsRead = true,
                Channel = CommunicationChannel.Sms,
                EventType = eventType,
                DeliveryStatus = smsDeliveryStatus,
                ErrorMessage = smsResult.ErrorMessage,
                SentAt = now
            };
            notificationsToSave.Add(smsLog);
        }

        _context.Notifications.AddRange(notificationsToSave);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<NotificationDto>> GetCustomerNotificationsAsync(int userId)
    {
        var notifications = await _context.Notifications
            .Include(n => n.Order)
            .Where(n => n.UserId == userId && n.Channel == CommunicationChannel.InApp)
            .OrderByDescending(n => n.SentAt)
            .ToListAsync();

        return notifications.Select(MapToDto);
    }

    public async Task<bool> MarkNotificationAsReadAsync(int notificationId, int userId)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification == null) return false;

        notification.IsRead = true;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<NotificationDto>> GetOrderCommunicationLogsAsync(int orderId)
    {
        var logs = await _context.Notifications
            .Include(n => n.Order)
            .Where(n => n.OrderId == orderId)
            .OrderBy(n => n.SentAt)
            .ToListAsync();

        return logs.Select(MapToDto);
    }

    private static string FormatSmsMessage(string trackingNumber, string eventType, string message)
    {
        return eventType switch
        {
            "OutForDelivery" => $"DeliveryTracker: Order {trackingNumber} is now Out for Delivery. Track your delivery in DeliveryTracker.",
            "DeliveryFailed" => $"DeliveryTracker: Delivery attempt for {trackingNumber} failed. Reason: {message}. Please open DeliveryTracker to reschedule.",
            "OrderDelivered" => $"DeliveryTracker: Order {trackingNumber} has been delivered successfully.",
            "OrderCreated" => $"DeliveryTracker: Order {trackingNumber} has been placed successfully.",
            "PickedUp" => $"DeliveryTracker: Order {trackingNumber} has been picked up from origin.",
            "InTransit" => $"DeliveryTracker: Order {trackingNumber} is now in transit.",
            "OrderRescheduled" => $"DeliveryTracker: Order {trackingNumber} has been rescheduled.",
            _ => $"DeliveryTracker: Order {trackingNumber} status update: {message}"
        };
    }

    private static bool ShouldSendSmsForEvent(string eventType)
    {
        // Dispatches on every status change lifecycle event
        return true;
    }

    private static NotificationDto MapToDto(Notification n) => new()
    {
        Id = n.Id,
        UserId = n.UserId,
        OrderId = n.OrderId,
        OrderTrackingNumber = n.Order?.TrackingNumber ?? $"Order #{n.OrderId}",
        Title = n.Title,
        Message = n.Message,
        RecipientEmail = n.RecipientEmail,
        RecipientPhone = n.RecipientPhone,
        IsRead = n.IsRead,
        Channel = n.Channel.ToString(),
        EventType = n.EventType,
        DeliveryStatus = n.DeliveryStatus.ToString(),
        ErrorMessage = n.ErrorMessage,
        SentAt = n.SentAt
    };
}
