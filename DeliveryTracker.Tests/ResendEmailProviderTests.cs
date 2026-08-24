using System.Net;
using System.Text.Json;
using DeliveryTracker.API.Services.Communication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DeliveryTracker.Tests;

public class ResendEmailProviderTests
{
    private class MockHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }
        private readonly HttpResponseMessage _response;

        public MockHttpMessageHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content != null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }
            return _response;
        }
    }

    [Fact]
    public async Task SendEmailAsync_WhenResendAcceptsEmail_ReturnsSuccessWithProviderId()
    {
        var responseJson = JsonSerializer.Serialize(new { id = "resend_msg_12345" });
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(mockHandler);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RESEND_API_KEY"] = "re_test_valid_api_key",
                ["HTTP_EMAIL_API_URL"] = "https://api.resend.com/emails",
                ["HTTP_EMAIL_FROM"] = "onboarding@resend.dev",
                ["HTTP_EMAIL_FROM_NAME"] = "DeliveryTracker Dispatch",
                ["NOTIFICATION_MODE"] = "Real",
                ["EMAIL_ENABLED"] = "true"
            })
            .Build();

        var provider = new ResendEmailProvider(httpClient, config, NullLogger<ResendEmailProvider>.Instance);

        var result = await provider.SendEmailAsync("customer@example.com", "Test Order Subject", "<p>Your order is confirmed</p>", "OrderCreated", 101);

        Assert.True(result.Success);
        Assert.Equal("resend_msg_12345", result.MessageId);
        Assert.Equal("ResendEmailProvider", result.Provider);
        Assert.Null(result.ErrorMessage);

        // Verify request payload
        Assert.NotNull(mockHandler.LastRequest);
        Assert.Equal("Bearer", mockHandler.LastRequest.Headers.Authorization?.Scheme);
        Assert.Equal("re_test_valid_api_key", mockHandler.LastRequest.Headers.Authorization?.Parameter);

        using var doc = JsonDocument.Parse(mockHandler.LastRequestBody!);
        Assert.Equal("DeliveryTracker Dispatch <onboarding@resend.dev>", doc.RootElement.GetProperty("from").GetString());
        Assert.Equal("customer@example.com", doc.RootElement.GetProperty("to")[0].GetString());
        Assert.Equal("Test Order Subject", doc.RootElement.GetProperty("subject").GetString());
    }

    [Fact]
    public async Task SendEmailAsync_WhenResendReturnsError_ReturnsFailureWithErrorMessage()
    {
        var responseJson = JsonSerializer.Serialize(new
        {
            statusCode = 422,
            message = "Domain not verified. You can only send to your own email address."
        });

        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
        {
            Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
        });

        var httpClient = new HttpClient(mockHandler);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RESEND_API_KEY"] = "re_test_key",
                ["NOTIFICATION_MODE"] = "Real",
                ["EMAIL_ENABLED"] = "true"
            })
            .Build();

        var provider = new ResendEmailProvider(httpClient, config, NullLogger<ResendEmailProvider>.Instance);

        var result = await provider.SendEmailAsync("unverified@example.com", "Order Update", "<p>Failed</p>", "OrderCreated", 102);

        Assert.False(result.Success);
        Assert.Equal("ResendEmailProvider", result.Provider);
        Assert.Contains("Domain not verified", result.ErrorMessage);
    }

    [Fact]
    public async Task SendEmailAsync_WhenApiKeyMissing_ReturnsFailureImmediately()
    {
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(mockHandler);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RESEND_API_KEY"] = "",
                ["NOTIFICATION_MODE"] = "Real",
                ["EMAIL_ENABLED"] = "true"
            })
            .Build();

        var provider = new ResendEmailProvider(httpClient, config, NullLogger<ResendEmailProvider>.Instance);

        var result = await provider.SendEmailAsync("customer@example.com", "Test", "<p>Body</p>", "OrderCreated", 103);

        Assert.False(result.Success);
        Assert.Equal("ResendEmailProvider", result.Provider);
        Assert.Contains("not configured", result.ErrorMessage);
        Assert.Null(mockHandler.LastRequest); // No HTTP request dispatched
    }

    [Fact]
    public async Task SendEmailAsync_WhenRecipientEmailInvalid_ReturnsFailure()
    {
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(mockHandler);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RESEND_API_KEY"] = "re_valid_key",
                ["NOTIFICATION_MODE"] = "Real",
                ["EMAIL_ENABLED"] = "true"
            })
            .Build();

        var provider = new ResendEmailProvider(httpClient, config, NullLogger<ResendEmailProvider>.Instance);

        var result = await provider.SendEmailAsync("not-an-email", "Test", "<p>Body</p>", "OrderCreated", 104);

        Assert.False(result.Success);
        Assert.Contains("Invalid recipient email", result.ErrorMessage);
        Assert.Null(mockHandler.LastRequest);
    }

    [Fact]
    public async Task SendEmailAsync_WhenEmailDisabled_ReturnsDisabledOk()
    {
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(mockHandler);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EMAIL_ENABLED"] = "false"
            })
            .Build();

        var provider = new ResendEmailProvider(httpClient, config, NullLogger<ResendEmailProvider>.Instance);

        var result = await provider.SendEmailAsync("customer@example.com", "Test", "<p>Body</p>", "OrderCreated");

        Assert.True(result.Success);
        Assert.Equal("DISABLED", result.MessageId);
        Assert.Null(mockHandler.LastRequest);
    }

    [Fact]
    public async Task SendEmailAsync_WhenSimulationMode_ReturnsSimulatedOkWithoutCallingHttp()
    {
        var mockHandler = new MockHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(mockHandler);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NOTIFICATION_MODE"] = "Simulation",
                ["EMAIL_ENABLED"] = "true"
            })
            .Build();

        var provider = new ResendEmailProvider(httpClient, config, NullLogger<ResendEmailProvider>.Instance);

        var result = await provider.SendEmailAsync("customer@example.com", "Test", "<p>Body</p>", "OrderCreated");

        Assert.True(result.Success);
        Assert.Equal("ResendEmailProvider(Simulated)", result.Provider);
        Assert.Null(mockHandler.LastRequest);
    }
}
