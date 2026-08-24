using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DeliveryTracker.API.Services.Communication;

public class ResendEmailProvider : IEmailNotificationProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ResendEmailProvider> _logger;

    public ResendEmailProvider(HttpClient httpClient, IConfiguration configuration, ILogger<ResendEmailProvider> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<CommunicationResult> SendEmailAsync(string recipientEmail, string subject, string body, string eventType, int? orderId = null)
    {
        var enabled = _configuration["EMAIL_ENABLED"] ?? _configuration["Notification:Email:Enabled"] ?? "true";
        if (enabled.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return CommunicationResult.Ok("ResendEmailProvider(Disabled)", "DISABLED");
        }

        var mode = _configuration["NOTIFICATION_MODE"] ?? _configuration["Notification:Mode"] ?? "Real";
        if (mode.Equals("Simulation", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("[RESEND SIMULATION] Simulating email to {Recipient}: {Subject}", recipientEmail, subject);
            return CommunicationResult.Ok("ResendEmailProvider(Simulated)", Guid.NewGuid().ToString("N")[..8]);
        }

        var apiKey = GetConfig("Notification:Email:Resend:ApiKey", "RESEND_API_KEY") 
            ?? GetConfig("Notification:Email:Http:ApiKey", "HTTP_EMAIL_API_KEY");
        
        var apiUrl = GetConfig("Notification:Email:Resend:ApiUrl", "HTTP_EMAIL_API_URL") 
            ?? "https://api.resend.com/emails";

        var fromEmail = GetConfig("Notification:Email:Resend:From", "HTTP_EMAIL_FROM") 
            ?? "onboarding@resend.dev";

        var fromName = GetConfig("Notification:Email:Resend:FromName", "HTTP_EMAIL_FROM_NAME") 
            ?? "DeliveryTracker Dispatch";

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.StartsWith("your_") || apiKey.StartsWith("PASTE_") || apiKey.StartsWith("YOUR_"))
        {
            _logger.LogWarning("Resend API key is unconfigured in Real notification mode.");
            return CommunicationResult.Fail("ResendEmailProvider", "Resend API key is not configured.");
        }

        if (string.IsNullOrWhiteSpace(recipientEmail) || !recipientEmail.Contains('@'))
        {
            return CommunicationResult.Fail("ResendEmailProvider", $"Invalid recipient email address: '{recipientEmail}'");
        }

        try
        {
            var senderFormatted = string.IsNullOrWhiteSpace(fromName)
                ? fromEmail
                : $"{fromName} <{fromEmail}>";

            var payload = new
            {
                from = senderFormatted,
                to = new[] { recipientEmail.Trim() },
                subject = subject,
                html = body
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl)
            {
                Content = jsonContent
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                string errorMessage = $"Resend HTTP {(int)response.StatusCode}: {responseContent}";
                try
                {
                    using var doc = JsonDocument.Parse(responseContent);
                    if (doc.RootElement.TryGetProperty("message", out var msgElem))
                    {
                        errorMessage = msgElem.GetString() ?? errorMessage;
                    }
                    else if (doc.RootElement.TryGetProperty("error", out var errElem))
                    {
                        errorMessage = errElem.GetString() ?? errorMessage;
                    }
                }
                catch
                {
                    // Keep raw response content if not JSON
                }

                _logger.LogError("Resend API error ({Status}) sending email to {Recipient}: {Error}", 
                    (int)response.StatusCode, recipientEmail, errorMessage);

                return CommunicationResult.Fail("ResendEmailProvider", errorMessage);
            }

            string messageId = Guid.NewGuid().ToString("N")[..8];
            try
            {
                using var doc = JsonDocument.Parse(responseContent);
                if (doc.RootElement.TryGetProperty("id", out var idElem))
                {
                    messageId = idElem.GetString() ?? messageId;
                }
            }
            catch
            {
                // Fallback to generated ID
            }

            _logger.LogInformation("[RESEND SUCCESS] Email accepted by Resend (ID: {MessageId}) for {Recipient}, Order #{OrderId}", 
                messageId, recipientEmail, orderId);

            return CommunicationResult.Ok("ResendEmailProvider", messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception communicating with Resend HTTPS API for recipient {Recipient}: {Message}", 
                recipientEmail, ex.Message);

            return CommunicationResult.Fail("ResendEmailProvider", ex.Message);
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
