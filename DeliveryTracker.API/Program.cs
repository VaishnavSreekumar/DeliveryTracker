using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using DeliveryTracker.API.Data;
using DeliveryTracker.API.Services;
using DeliveryTracker.API.Services.Communication;
// Load local environment variables from .env.local and .env if present (Server-side only)
LoadLocalEnvironmentFile(Path.Combine(Directory.GetCurrentDirectory(), ".env.local"));
LoadLocalEnvironmentFile(Path.Combine(Directory.GetCurrentDirectory(), ".env"));
LoadLocalEnvironmentFile(Path.Combine(AppContext.BaseDirectory, ".env.local"));

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger
builder.Services.AddSwaggerGen();

// Configure DbContext based on connection string (PostgreSQL in production, SQLite in local dev)
var rawConnStr = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? "Data Source=delivery.db";

bool isPostgres = rawConnStr.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) 
    || rawConnStr.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
    || rawConnStr.Contains("Host=", StringComparison.OrdinalIgnoreCase)
    || rawConnStr.Contains("Server=", StringComparison.OrdinalIgnoreCase)
    || string.Equals(Environment.GetEnvironmentVariable("DB_PROVIDER"), "PostgreSQL", StringComparison.OrdinalIgnoreCase);

if (isPostgres)
{
    var postgresConnStr = ConvertPostgresUrlToConnectionString(rawConnStr);
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(postgresConnStr));
}
else
{
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(rawConnStr));
}

// Configure JWT Bearer Authentication
var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] 
    ?? "DeliveryTrackerSuperSecretKey2026!Format32BytesLengthKey";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "DeliveryTrackerAPI",
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "DeliveryTrackerClients",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Configure CORS for React frontend client (Development + Production Vercel domain)
var allowedOriginsEnv = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS") 
    ?? builder.Configuration["Cors:AllowedOrigins"] 
    ?? "";

var customOrigins = new List<string>();
if (!string.IsNullOrWhiteSpace(allowedOriginsEnv))
{
    var extraOrigins = allowedOriginsEnv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    customOrigins.AddRange(extraOrigins);
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.SetIsOriginAllowed(origin =>
        {
            if (string.IsNullOrWhiteSpace(origin)) return false;
            try
            {
                var uri = new Uri(origin);
                return uri.Host == "localhost" 
                    || uri.Host == "127.0.0.1"
                    || uri.Host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase)
                    || customOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        })
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

// Register Services
builder.Services.AddScoped<IPricingService, PricingService>();

// Email Provider Selection: HTTP (Resend HTTPS API for production) vs SMTP (Local development)
var emailProviderType = Environment.GetEnvironmentVariable("EMAIL_PROVIDER") 
    ?? builder.Configuration["Notification:Email:Provider"] 
    ?? "SMTP";

if (emailProviderType.Equals("HTTP", StringComparison.OrdinalIgnoreCase) 
    || emailProviderType.Equals("RESEND", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<IEmailNotificationProvider, ResendEmailProvider>();
}
else
{
    builder.Services.AddScoped<IEmailNotificationProvider, SmtpEmailProvider>();
}

builder.Services.AddScoped<ISmsNotificationProvider, TwilioSmsProvider>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IAgentAssignmentService, AgentAssignmentService>();
builder.Services.AddScoped<IOrderStatusService, OrderStatusService>();
builder.Services.AddScoped<IDeliveryRecoveryService, DeliveryRecoveryService>();
builder.Services.AddScoped<IAuthService, AuthService>();

var app = builder.Build();

// Configure HTTP request pipeline.
if (app.Environment.IsDevelopment() || true) // Ensure Swagger is enabled for demo/testing
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Last-Mile Delivery Tracker API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Lightweight Health Check Endpoint for Render / zero-downtime monitoring
app.MapGet("/api/health", () => Results.Ok(new 
{ 
    status = "healthy", 
    service = "DeliveryTracker.API", 
    timestamp = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName
}));

// Initialize database with migrations and seed data
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    DbInitializer.Initialize(dbContext);
}

// Bind to Render PORT if set and no URLs explicitly configured
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port) && !app.Urls.Any())
{
    app.Urls.Add($"http://0.0.0.0:{port}");
}

app.Run();

static void LoadLocalEnvironmentFile(string filePath)
{
    if (!File.Exists(filePath)) return;
    try
    {
        foreach (var line in File.ReadAllLines(filePath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#")) continue;
            var idx = trimmed.IndexOf('=');
            if (idx > 0)
            {
                var key = trimmed[..idx].Trim();
                var val = trimmed[(idx + 1)..].Trim().Trim('"', '\'');
                Environment.SetEnvironmentVariable(key, val);
            }
        }
    }
    catch
    {
        // Safe fallback
    }
}

static string ConvertPostgresUrlToConnectionString(string urlOrConn)
{
    if (!urlOrConn.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
        !urlOrConn.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        return urlOrConn;
    }

    try
    {
        var uri = new Uri(urlOrConn);
        var userInfo = uri.UserInfo.Split(':');
        var user = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var port = uri.Port > 0 ? uri.Port : 5432;
        var database = uri.AbsolutePath.TrimStart('/');

        return $"Host={uri.Host};Port={port};Database={database};Username={user};Password={password};SSL Mode=Require;Trust Server Certificate=true";
    }
    catch
    {
        return urlOrConn;
    }
}
