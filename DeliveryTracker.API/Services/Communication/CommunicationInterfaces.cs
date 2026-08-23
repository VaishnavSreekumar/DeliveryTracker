namespace DeliveryTracker.API.Services.Communication;

public class CommunicationResult
{
    public bool Success { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? MessageId { get; set; }
    public string? ErrorMessage { get; set; }

    public static CommunicationResult Ok(string provider, string? messageId = null) => new()
    {
        Success = true,
        Provider = provider,
        MessageId = messageId
    };

    public static CommunicationResult Fail(string provider, string errorMessage) => new()
    {
        Success = false,
        Provider = provider,
        ErrorMessage = errorMessage
    };
}

public interface IEmailNotificationProvider
{
    Task<CommunicationResult> SendEmailAsync(string recipientEmail, string subject, string body, string eventType, int? orderId = null);
}

public interface ISmsNotificationProvider
{
    Task<CommunicationResult> SendSmsAsync(string recipientPhone, string message, string eventType, int? orderId = null);
}
