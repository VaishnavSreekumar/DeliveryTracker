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

// Configure SQLite DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=delivery.db";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

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

// Configure CORS for React frontend client
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

// Register Services
builder.Services.AddScoped<IPricingService, PricingService>();
builder.Services.AddScoped<IEmailNotificationProvider, SmtpEmailProvider>();
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

// Initialize database with migrations and seed data
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    DbInitializer.Initialize(dbContext);
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
