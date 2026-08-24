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

        var mode = _configuration["NOTIFICATION_MODE"] ?? _configuration["Notification:Mode"] ?? "Real";
        if (mode.Equals("Simulation", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("[SMTP SIMULATION] Explicit simulation mode. Simulating email to {Recipient}: {Subject}", recipientEmail, subject);
            return CommunicationResult.Ok("SmtpEmailProvider(Simulated)", Guid.NewGuid().ToString("N")[..8]);
        }

        var host = GetConfig("Notification:Email:Smtp:Host", "SMTP_HOST");
        var portStr = GetConfig("Notification:Email:Smtp:Port", "SMTP_PORT") ?? "587";
        var username = GetConfig("Notification:Email:Smtp:Username", "SMTP_USERNAME");
        var password = GetConfig("Notification:Email:Smtp:Password", "SMTP_PASSWORD");
        var fromAddress = GetConfig("Notification:Email:FromAddress", "SMTP_FROM") ?? GetConfig("NOTIFICATION_EMAIL_FROM", "NOTIFICATION_EMAIL_FROM") ?? "notifications@deliverytracker.com";

        int port = 587;
        bool isConfigured = !string.IsNullOrWhiteSpace(host)
            && int.TryParse(portStr, out port)
            && !string.IsNullOrWhiteSpace(username)
            && !username.StartsWith("your_")
            && !username.StartsWith("PASTE_")
            && !username.StartsWith("YOUR_")
            && !string.IsNullOrWhiteSpace(password)
            && !password.StartsWith("your_")
            && !password.StartsWith("PASTE_")
            && !password.StartsWith("YOUR_");

        if (!isConfigured)
        {
            _logger.LogWarning("SMTP credentials unconfigured in Real notification mode.");
            return CommunicationResult.Fail("SmtpEmailProvider", "SMTP credentials not configured for Real notification mode.");
        }

        try
        {
            using var client = new SmtpClient(host, port)
            {
                EnableSsl = true,
                Timeout = 15000,
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

    private string? GetConfig(string configKey, string envKey)
    {
        var envVal = Environment.GetEnvironmentVariable(envKey);
        if (!string.IsNullOrWhiteSpace(envVal)) return envVal.Trim();

        var direct = _configuration[envKey];
        if (!string.IsNullOrWhiteSpace(direct)) return direct.Trim();

        var nested = _configuration[configKey];
        if (!string.IsNullOrWhiteSpace(nested)) return nested.Trim();

        return null;
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
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    }

    public async Task<CommunicationResult> SendSmsAsync(string recipientPhone, string message, string eventType, int? orderId = null)
    {
        var enabled = GetConfig("Notification:Sms:Enabled", "SMS_ENABLED") ?? "true";
        if (enabled.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return CommunicationResult.Ok("TwilioSmsProvider(Disabled)", "DISABLED");
        }

        var mode = GetConfig("Notification:Mode", "NOTIFICATION_MODE") ?? "Real";
        if (mode.Equals("Simulation", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("[TWILIO SIMULATION] Explicit simulation mode. Simulating SMS to {Phone}: {Message}", recipientPhone, message);
            return CommunicationResult.Ok("TwilioSmsProvider(Simulated)", Guid.NewGuid().ToString("N")[..8]);
        }

        var accountSid = GetConfig("Notification:Sms:Twilio:AccountSid", "TWILIO_ACCOUNT_SID");
        var apiKey = GetConfig("TWILIO_API_KEY", "TWILIO_API_KEY");
        var apiSecret = GetConfig("TWILIO_API_SECRET", "TWILIO_API_SECRET");
        var authToken = GetConfig("Notification:Sms:Twilio:AuthToken", "TWILIO_AUTH_TOKEN");
        var fromNumber = GetConfig("Notification:Sms:FromNumber", "TWILIO_FROM_NUMBER") ?? GetConfig("NOTIFICATION_SMS_FROM", "NOTIFICATION_SMS_FROM") ?? "+18005550199";

        // Determine basic auth credentials: (ApiKey:ApiSecret) OR (AccountSid:AuthToken)
        string authUser = !string.IsNullOrWhiteSpace(apiKey) && !apiKey.StartsWith("PASTE_") && !apiKey.StartsWith("YOUR_")
            ? apiKey
            : accountSid ?? "";

        string authSecret = !string.IsNullOrWhiteSpace(apiSecret) && !apiSecret.StartsWith("PASTE_") && !apiSecret.StartsWith("YOUR_")
            ? apiSecret
            : authToken ?? "";

        bool isConfigured = !string.IsNullOrWhiteSpace(accountSid)
            && !accountSid.StartsWith("your_")
            && !accountSid.StartsWith("PASTE_")
            && !accountSid.StartsWith("YOUR_")
            && !string.IsNullOrWhiteSpace(authUser)
            && !string.IsNullOrWhiteSpace(authSecret)
            && !authSecret.StartsWith("your_")
            && !authSecret.StartsWith("PASTE_")
            && !authSecret.StartsWith("YOUR_");

        if (!isConfigured)
        {
            _logger.LogWarning("Twilio credentials unconfigured in Real notification mode.");
            return CommunicationResult.Fail("TwilioSmsProvider", "Twilio credentials not configured for Real notification mode.");
        }

        try
        {
            var normalizedFrom = fromNumber.Trim();
            if (!normalizedFrom.StartsWith("+"))
            {
                normalizedFrom = "+" + normalizedFrom;
            }

            var normalizedTo = recipientPhone.Trim().Replace(" ", "").Replace("-", "");
            if (!normalizedTo.StartsWith("+"))
            {
                normalizedTo = "+" + normalizedTo;
            }

            var requestUrl = $"https://api.twilio.com/2010-04-01/Accounts/{accountSid}/Messages.json";
            var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            var byteArray = System.Text.Encoding.ASCII.GetBytes($"{authUser}:{authSecret}");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

            var configuredTemplate = GetConfig("Notification:Sms:Twilio:TrialTemplate", "TWILIO_TRIAL_TEMPLATE");
            var smsBody = !string.IsNullOrWhiteSpace(configuredTemplate) ? configuredTemplate : "sms_order_confirmation";

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("To", normalizedTo),
                new KeyValuePair<string, string>("From", normalizedFrom),
                new KeyValuePair<string, string>("Body", smsBody)
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

    private string? GetConfig(string configKey, string envKey)
    {
        var envVal = Environment.GetEnvironmentVariable(envKey);
        if (!string.IsNullOrWhiteSpace(envVal)) return envVal.Trim();

        var direct = _configuration[envKey];
        if (!string.IsNullOrWhiteSpace(direct)) return direct.Trim();

        var nested = _configuration[configKey];
        if (!string.IsNullOrWhiteSpace(nested)) return nested.Trim();

        return null;
    }
}
