using System.Security.Claims;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Roam.Api.Endpoints;
using Roam.Api.Hubs;
using Roam.Api.Middleware;
using Roam.Application.Places;
using Roam.Application.TourRequests;
using Roam.Application.TrustAndSafety;
using Roam.Domain.Users;
using Roam.Infrastructure.Maps;
using Roam.Infrastructure.Persistence;
using Roam.Infrastructure.Places;
using Roam.Infrastructure.TourRequests;
using Roam.Infrastructure.TrustAndSafety;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// Add Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        x => x.UseNetTopologySuite()));

// Configure Identity with lockout and password rules
builder.Services.AddIdentityApiEndpoints<ApplicationUser>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 8;
    options.Password.RequiredUniqueChars = 1;

    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    options.User.RequireUniqueEmail = true;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// Add Auth & Policies
builder.Services.AddAuthentication();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("Admin"));
    options.AddPolicy("RequireApprovedVolunteer", policy => policy.RequireClaim("VolunteerStatus", "Approved"));
});

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("AuthPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy("UserPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy("ReportsPolicy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 15,
                Window = TimeSpan.FromMinutes(1)
            }));
});

// Problem Details
builder.Services.AddProblemDetails();

// Add Places Services
builder.Services.AddHttpClient<IGeocodingService, OsmGeocodingService>();
builder.Services.AddScoped<IPlaceService, PlaceService>();
builder.Services.AddScoped<ITourRequestService, TourRequestService>();
builder.Services.AddScoped<ITrustAndSafetyService, TrustAndSafetyService>();
builder.Services.AddScoped<Roam.Application.Chat.IChatService, Roam.Infrastructure.Chat.ChatService>();
builder.Services.AddScoped<ITourNotificationService, Roam.Api.Services.SignalRTourNotificationService>();
builder.Services.AddScoped<Roam.Application.TrustAndSafety.IFeedbackService, Roam.Infrastructure.TrustAndSafety.FeedbackService>();
builder.Services.AddScoped<Roam.Application.Payments.IPaymentService, Roam.Infrastructure.Payments.PaymentService>();

// TURN Server configuration
builder.Services.Configure<Roam.Application.Sessions.TurnServerOptions>(builder.Configuration.GetSection(Roam.Application.Sessions.TurnServerOptions.Position));
builder.Services.AddScoped<Roam.Application.Sessions.ITurnService, Roam.Infrastructure.Realtime.TurnService>();

// Add SignalR
builder.Services.AddSignalR();

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>();

// API Versioning
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
})
.AddMvc()
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// Add OpenAPI (Swagger)
builder.Services.AddOpenApi();

// Configurable CORS
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173", "https://localhost:5173" };

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Build app
var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseCors();
app.UseRateLimiter();

app.UseAuthentication();
app.UseMiddleware<AccountStatusMiddleware>();
app.UseAuthorization();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment()) 
{
    app.MapOpenApi();
}

// Only redirect HTTPS when not running behind reverse proxy or in production container
if (!app.Environment.IsProduction() && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ENABLE_HTTPS_REDIRECT")))
{
    app.UseHttpsRedirection();
}

// Map Health Checks
app.MapHealthChecks("/health");

// Shared API Version Set
var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

app.MapPlacesEndpoints();
app.MapTourRequestEndpoints();
app.MapTrustAndSafetyEndpoints();
app.MapUserProfileEndpoints();
app.MapSessionEndpoints();
app.MapChatEndpoints();
app.MapPaymentsEndpoints();
app.MapIdentityApi<ApplicationUser>();

app.MapGet("/", () => "Roam API is running").WithApiVersionSet(apiVersionSet);

app.MapHub<TourHub>("/hubs/tour");
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<PresenceHub>("/hubs/presence");

// Apply migrations safely on startup unless disabled
if (Environment.GetEnvironmentVariable("SKIP_MIGRATIONS") != "true")
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.Run();

public partial class Program { }
