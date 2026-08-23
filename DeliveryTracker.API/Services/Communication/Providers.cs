using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DeliveryTracker.API.Services.Communication;

public class DevelopmentEmailProvider : IEmailNotificationProvider
{
    private readonly ILogger<DevelopmentEmailProvider> _logger;

    public DevelopmentEmailProvider(ILogger<DevelopmentEmailProvider> logger)
    {
        _logger = logger;
    }

    public Task<CommunicationResult> SendEmailAsync(string recipientEmail, string subject, string body, string eventType, int? orderId = null)
    {
        _logger.LogInformation("[DEV EMAIL SIMULATION] To: {Recipient} | Event: {Event} | Order: {OrderId} | Subject: {Subject}",
            recipientEmail, eventType, orderId, subject);

        return Task.FromResult(CommunicationResult.Ok("DevelopmentEmailProvider", Guid.NewGuid().ToString("N")[..8]));
    }
}

public class SmtpEmailProvider : IEmailNotificationProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailProvider> _logger;

    public SmtpEmailProvider(IConfiguration configuration, ILogger<SmtpEmailProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<CommunicationResult> SendEmailAsync(string recipientEmail, string subject, string body, string eventType, int? orderId = null)
    {
        var host = _configuration["Notification:Email:Smtp:Host"];
        var portStr = _configuration["Notification:Email:Smtp:Port"];
        var username = _configuration["Notification:Email:Smtp:Username"];
        var password = _configuration["Notification:Email:Smtp:Password"];
        var fromAddress = _configuration["Notification:Email:FromAddress"] ?? "notifications@deliverytracker.com";

        if (string.IsNullOrWhiteSpace(host) || !int.TryParse(portStr, out int port))
        {
            _logger.LogWarning("SMTP configuration incomplete. Falling back to development simulated email.");
            return CommunicationResult.Ok("SmtpEmailProvider(Simulated)", Guid.NewGuid().ToString("N")[..8]);
        }

        try
        {
            using var client = new SmtpClient(host, port)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(username, password)
            };

            var mail = new MailMessage(fromAddress, recipientEmail, subject, body)
            {
                IsBodyHtml = true
            };

            await client.SendMailAsync(mail);
            return CommunicationResult.Ok("SmtpEmailProvider", Guid.NewGuid().ToString("N")[..8]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send real SMTP email to {Recipient}", recipientEmail);
            return CommunicationResult.Fail("SmtpEmailProvider", ex.Message);
        }
    }
}

public class DevelopmentSmsProvider : ISmsNotificationProvider
{
    private readonly ILogger<DevelopmentSmsProvider> _logger;

    public DevelopmentSmsProvider(ILogger<DevelopmentSmsProvider> logger)
    {
        _logger = logger;
    }

    public Task<CommunicationResult> SendSmsAsync(string recipientPhone, string message, string eventType, int? orderId = null)
    {
        _logger.LogInformation("[DEV SMS SIMULATION] To: {Phone} | Event: {Event} | Order: {OrderId} | Message: {Message}",
            recipientPhone, eventType, orderId, message);

        return Task.FromResult(CommunicationResult.Ok("DevelopmentSmsProvider", Guid.NewGuid().ToString("N")[..8]));
    }
}
