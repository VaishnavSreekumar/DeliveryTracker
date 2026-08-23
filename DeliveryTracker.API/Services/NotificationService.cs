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

        // Resolve Email & Phone from user if not explicitly passed
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            var user = await _context.Users.FindAsync(userId);
            recipientEmail = user?.Email ?? "customer@delivery.com";
        }
        recipientPhone ??= "+91 98765 43210";

        var notificationsToSave = new List<Notification>();

        // 1. IN-APP Notification Channel (Only if not already created)
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
            string emailSubject = $"[DeliveryTracker] {title}";
            string emailBody = $@"
                <h3>Delivery Update: {trackingNumber}</h3>
                <p><strong>Status Event:</strong> {eventType}</p>
                <p>{message}</p>
                <hr/>
                <p><small>Track live at DeliveryTracker platform</small></p>";

            emailResult = await _emailProvider.SendEmailAsync(recipientEmail, emailSubject, emailBody, eventType, orderId);
        }
        catch (Exception ex)
        {
            emailResult = CommunicationResult.Fail("EmailProvider", ex.Message);
        }

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
            DeliveryStatus = emailResult.Success ? CommunicationStatus.Simulated : CommunicationStatus.Failed,
            ErrorMessage = emailResult.ErrorMessage,
            SentAt = now
        };
        notificationsToSave.Add(emailLog);

        // 3. SMS Notification Channel (For critical lifecycle events)
        if (ShouldSendSmsForEvent(eventType))
        {
            CommunicationResult smsResult;
            try
            {
                string smsMessage = $"[DeliveryTracker] Order {trackingNumber}: {message}";
                smsResult = await _smsProvider.SendSmsAsync(recipientPhone, smsMessage, eventType, orderId);
            }
            catch (Exception ex)
            {
                smsResult = CommunicationResult.Fail("SmsProvider", ex.Message);
            }

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
                DeliveryStatus = smsResult.Success ? CommunicationStatus.Simulated : CommunicationStatus.Failed,
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

    private static bool ShouldSendSmsForEvent(string eventType)
    {
        return eventType switch
        {
            "OrderCreated" or "OutForDelivery" or "DeliveryFailed" or "OrderRescheduled" or "OrderDelivered" => true,
            _ => false
        };
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
