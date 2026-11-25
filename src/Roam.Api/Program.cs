using System.Security.Claims;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Roam.Api.Endpoints;
using Roam.Api.Hubs;
using Roam.Api.Middleware;
using Roam.Api.Scaling;
using Roam.Api.Security;
using Roam.Api.Workers;
using Roam.Application.Places;
using Roam.Application.TourRequests;
using Roam.Application.TrustAndSafety;
using Roam.Domain.Users;
using Roam.Infrastructure.Maps;
using Roam.Infrastructure.Persistence;
using Roam.Infrastructure.Places;
using Roam.Infrastructure.Scaling;
using Roam.Infrastructure.TourRequests;
using Roam.Infrastructure.TrustAndSafety;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// Add Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        x => x.UseNetTopologySuite());
});

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
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddClaimsPrincipalFactory<Roam.Infrastructure.Identity.ApplicationUserClaimsPrincipalFactory>();

// Identity Email Sender
builder.Services.AddTransient<IEmailSender<ApplicationUser>, Roam.Api.Services.SmtpEmailSender>();

// Data Protection key ring. Bearer access/refresh tokens are data-protected, so every API instance must
// share the same keys or a token issued by one server is rejected by another. Keys are stored in
// PostgreSQL by default; DATA_PROTECTION_KEYS_PATH opts back into a (single-server) folder.
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("Roam");
var keysFolder = Environment.GetEnvironmentVariable("DATA_PROTECTION_KEYS_PATH");
if (!string.IsNullOrWhiteSpace(keysFolder))
{
    Directory.CreateDirectory(keysFolder);
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysFolder));
}
else
{
    dataProtection.PersistKeysToDbContext<ApplicationDbContext>();
}

// Encrypt the key ring at rest: anyone who can read the database (or a backup of it) could otherwise
// use the keys to forge sign-in tokens. DATA_PROTECTION_CERT_BASE64 is a base64 PKCS#12 (.pfx) file.
var keyEncryptionCertificate = DataProtectionCertificate.Load(
    builder.Configuration["DATA_PROTECTION_CERT_BASE64"], builder.Configuration["DATA_PROTECTION_CERT_PASSWORD"]);
if (keyEncryptionCertificate is not null)
{
    dataProtection.ProtectKeysWithCertificate(keyEncryptionCertificate)
        .UnprotectKeysWithAnyCertificate(keyEncryptionCertificate);
}
else if (builder.Environment.IsProduction() && builder.Configuration["DATA_PROTECTION_ALLOW_UNENCRYPTED_KEYS"] != "true")
{
    throw new InvalidOperationException(
        "DATA_PROTECTION_CERT_BASE64 must be set in Production so sign-in keys are encrypted at rest " +
        "(scripts/setup-env.sh generates it). Set DATA_PROTECTION_ALLOW_UNENCRYPTED_KEYS=true to run without it.");
}

// State shared across API instances (Redis when ConnectionStrings:Redis is set, in-memory otherwise).
var redisConn = builder.Configuration.GetConnectionString("Redis");
var redis = builder.Services.AddRoamScaleOut(redisConn);

// Add Auth & Policies
builder.Services.AddAuthentication();
// Access tokens embed VolunteerStatus/AccountStatus/role claims at issue time; refresh them from the
// database (briefly cached) on every request so permission changes apply without forcing re-login.
builder.Services.AddSingleton<IUserStateCache, UserStateCache>();
builder.Services.AddScoped<IClaimsTransformation, UserStateClaimsTransformation>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole", policy => policy.RequireRole("Admin"));
    options.AddPolicy("RequireApprovedVolunteer", policy => policy.RequireClaim("VolunteerStatus", "Approved"));
});

// Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // With Redis configured, counters are shared by all instances; otherwise they are per process.
    RateLimitPartition<string> FixedWindow(HttpContext httpContext, string policy, string partitionKey, int permitLimit) =>
        redis is null
            ? RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1)
            })
            : RateLimitPartition.Get(partitionKey, key => new RedisFixedWindowRateLimiter(
                redis, $"roam:ratelimit:{policy}:{key}", permitLimit, TimeSpan.FromMinutes(1),
                httpContext.RequestServices.GetService<ILoggerFactory>()?.CreateLogger<RedisFixedWindowRateLimiter>()));

    static string UserOrIp(HttpContext httpContext) =>
        httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon";

    options.AddPolicy("AuthPolicy", httpContext =>
        FixedWindow(httpContext, "auth", httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", 10));

    options.AddPolicy("UserPolicy", httpContext =>
        FixedWindow(httpContext, "user", UserOrIp(httpContext), 60));

    options.AddPolicy("ReportsPolicy", httpContext =>
        FixedWindow(httpContext, "reports", UserOrIp(httpContext), 15));
});

// Problem Details
builder.Services.AddProblemDetails();

// Add Places Services with configured timeout
builder.Services.AddHttpClient<IGeocodingService, OsmGeocodingService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddScoped<IPlaceService, PlaceService>();
builder.Services.AddScoped<ITourRequestService, TourRequestService>();
builder.Services.AddHostedService<TourRequestExpirationWorker>();
builder.Services.AddScoped<ITrustAndSafetyService, TrustAndSafetyService>();
builder.Services.AddScoped<Roam.Application.Chat.IChatService, Roam.Infrastructure.Chat.ChatService>();
builder.Services.AddScoped<ITourNotificationService, Roam.Api.Services.SignalRTourNotificationService>();
builder.Services.AddScoped<Roam.Application.TrustAndSafety.IFeedbackService, Roam.Infrastructure.TrustAndSafety.FeedbackService>();
builder.Services.AddScoped<Roam.Application.Payments.IPaymentService, Roam.Infrastructure.Payments.PaymentService>();

// TURN Server configuration
builder.Services.Configure<Roam.Application.Sessions.TurnServerOptions>(builder.Configuration.GetSection(Roam.Application.Sessions.TurnServerOptions.Position));
builder.Services.AddHttpClient<Roam.Application.Sessions.ITurnService, Roam.Infrastructure.Realtime.TurnService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Add In-Memory Cache
builder.Services.AddMemoryCache();

// Add SignalR with Security Filter and, for multiple instances, the Redis backplane so groups,
// Clients.User(...) and Clients.Client(...) reach connections held by any server.
var signalRBuilder = builder.Services.AddSignalR(options =>
{
    options.AddFilter<SignalRSecurityFilter>();
});

if (redis is not null)
{
    signalRBuilder.AddStackExchangeRedis(options =>
    {
        options.Configuration.ChannelPrefix = RedisChannel.Literal("roam:signalr");
        options.ConnectionFactory = _ => Task.FromResult(redis);
    });
}
builder.Services.AddSingleton<SignalRSecurityFilter>();
builder.Services.AddSingleton<ITourConnectionRegistry, DistributedTourConnectionRegistry>();

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

// SignalR access_token query string authentication support (respect existing Authorization header)
app.Use(async (context, next) =>
{
    if (string.IsNullOrEmpty(context.Request.Headers.Authorization))
    {
        var accessToken = context.Request.Query["access_token"];
        var path = context.Request.Path;
        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
        {
            context.Request.Headers.Append("Authorization", $"Bearer {accessToken}");
        }
    }
    await next();
});

app.UseAuthentication();
app.UseRateLimiter();
app.UseMiddleware<AccountStatusMiddleware>();
app.UseAuthorization();

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
});

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
app.MapAdminEndpoints();
app.MapSessionEndpoints();
app.MapChatEndpoints();
app.MapPaymentsEndpoints();
app.MapIdentityApi<ApplicationUser>().RequireRateLimiting("AuthPolicy");

app.MapGet("/", () => "Roam API is running").WithApiVersionSet(apiVersionSet);

app.MapHub<TourHub>("/hubs/tour");
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<PresenceHub>("/hubs/presence");

// Apply migrations and seed safely on startup unless disabled
if (Environment.GetEnvironmentVariable("SKIP_MIGRATIONS") != "true")
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    // Instances starting at the same time would otherwise apply the same migration twice and crash.
    await PostgresAdvisoryLock.RunExclusiveAsync(dbContext, PostgresAdvisoryLock.MigrationLockId,
        ct => dbContext.Database.MigrateAsync(ct), CancellationToken.None);
    await Roam.Infrastructure.Identity.DatabaseSeeder.SeedAsync(app.Services, app.Configuration);
}

app.Run();

public partial class Program { }
