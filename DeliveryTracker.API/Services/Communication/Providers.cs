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
        _logger.LogInformation("[EMAIL SIMULATION] To: {Recipient} | Event: {Event} | Order: {OrderId} | Subject: {Subject}",
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
        var enabled = _configuration["EMAIL_ENABLED"] ?? _configuration["Notification:Email:Enabled"] ?? "true";
        if (enabled.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return CommunicationResult.Ok("SmtpEmailProvider(Disabled)", "DISABLED");
        }

        var host = _configuration["Notification:Email:Smtp:Host"] ?? _configuration["SMTP_HOST"];
        var portStr = _configuration["Notification:Email:Smtp:Port"] ?? _configuration["SMTP_PORT"];
        var username = _configuration["Notification:Email:Smtp:Username"] ?? _configuration["SMTP_USERNAME"];
        var password = _configuration["Notification:Email:Smtp:Password"] ?? _configuration["SMTP_PASSWORD"];
        var fromAddress = _configuration["Notification:Email:FromAddress"] ?? _configuration["SMTP_FROM"] ?? _configuration["NOTIFICATION_EMAIL_FROM"] ?? "notifications@deliverytracker.com";

        if (string.IsNullOrWhiteSpace(host) || !int.TryParse(portStr, out int port) || string.IsNullOrWhiteSpace(username) || username.StartsWith("your_"))
        {
            _logger.LogInformation("[SMTP SIMULATION] Credentials unconfigured. Simulating email to {Recipient}: {Subject}", recipientEmail, subject);
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
            _logger.LogInformation("[SMTP SUCCESS] Real email sent to {Recipient} for order {OrderId}", recipientEmail, orderId);
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
        _logger.LogInformation("[SMS SIMULATION] To: {Phone} | Event: {Event} | Order: {OrderId} | Message: {Message}",
            recipientPhone, eventType, orderId, message);

        return Task.FromResult(CommunicationResult.Ok("DevelopmentSmsProvider", Guid.NewGuid().ToString("N")[..8]));
    }
}

public class TwilioSmsProvider : ISmsNotificationProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TwilioSmsProvider> _logger;
    private readonly HttpClient _httpClient;

    public TwilioSmsProvider(IConfiguration configuration, ILogger<TwilioSmsProvider> logger, HttpClient? httpClient = null)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<CommunicationResult> SendSmsAsync(string recipientPhone, string message, string eventType, int? orderId = null)
    {
        var enabled = _configuration["SMS_ENABLED"] ?? _configuration["Notification:Sms:Enabled"] ?? "true";
        if (enabled.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return CommunicationResult.Ok("TwilioSmsProvider(Disabled)", "DISABLED");
        }

        var accountSid = _configuration["Notification:Sms:Twilio:AccountSid"] ?? _configuration["TWILIO_ACCOUNT_SID"];
        var authToken = _configuration["Notification:Sms:Twilio:AuthToken"] ?? _configuration["TWILIO_AUTH_TOKEN"];
        var fromNumber = _configuration["Notification:Sms:FromNumber"] ?? _configuration["TWILIO_FROM_NUMBER"] ?? _configuration["NOTIFICATION_SMS_FROM"] ?? "+18005550199";

        if (string.IsNullOrWhiteSpace(accountSid) || string.IsNullOrWhiteSpace(authToken) || accountSid.StartsWith("your_"))
        {
            _logger.LogInformation("[TWILIO SIMULATION] Account SID unconfigured. Simulating SMS to {Phone}: {Message}", recipientPhone, message);
            return CommunicationResult.Ok("TwilioSmsProvider(Simulated)", Guid.NewGuid().ToString("N")[..8]);
        }

        try
        {
            var requestUrl = $"https://api.twilio.com/2010-04-01/Accounts/{accountSid}/Messages.json";
            var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            var byteArray = System.Text.Encoding.ASCII.GetBytes($"{accountSid}:{authToken}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("To", recipientPhone),
                new KeyValuePair<string, string>("From", fromNumber),
                new KeyValuePair<string, string>("Body", message)
            });
            request.Content = formContent;

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[TWILIO SUCCESS] Real SMS dispatched to {Phone} for order {OrderId}", recipientPhone, orderId);
                return CommunicationResult.Ok("TwilioSmsProvider", Guid.NewGuid().ToString("N")[..8]);
            }

            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogError("Twilio API error: {Error}", errorBody);
            return CommunicationResult.Fail("TwilioSmsProvider", $"HTTP {response.StatusCode}: {errorBody}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Twilio SMS to {Phone}", recipientPhone);
            return CommunicationResult.Fail("TwilioSmsProvider", ex.Message);
        }
    }
}
