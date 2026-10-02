using Asp.Versioning;
using Microsoft.EntityFrameworkCore;
using Roam.Infrastructure.Persistence;
using Roam.Application.Places;
using Roam.Application.TourRequests;
using Roam.Application.TrustAndSafety;
using Roam.Infrastructure.Places;
using Roam.Infrastructure.TourRequests;
using Roam.Infrastructure.TrustAndSafety;
using Roam.Infrastructure.Maps;
using Roam.Domain.Users;
using Roam.Api.Endpoints;
using Roam.Api.Hubs;
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

// Configure Identity
builder.Services.AddIdentityApiEndpoints<ApplicationUser>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Add Auth
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

// Add Places Services
builder.Services.AddHttpClient<IGeocodingService, OsmGeocodingService>();
builder.Services.AddScoped<IPlaceService, PlaceService>();
builder.Services.AddScoped<ITourRequestService, TourRequestService>();
builder.Services.AddScoped<ITrustAndSafetyService, TrustAndSafetyService>();
builder.Services.AddScoped<ITourNotificationService, Roam.Api.Services.SignalRTourNotificationService>();

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

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173", "https://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Build app
var app = builder.Build();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment()) 
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Map Health Checks
app.MapHealthChecks("/health");

app.MapPlacesEndpoints();
app.MapTourRequestEndpoints();
app.MapTrustAndSafetyEndpoints();
app.MapUserProfileEndpoints();
app.MapSessionEndpoints();
app.MapIdentityApi<ApplicationUser>();

app.MapGet("/", () => "Roam API is running").WithApiVersionSet(app.NewApiVersionSet().Build());

app.MapHub<TourHub>("/hubs/tour");
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<ChatHub>("/hubs/chat");
app.MapHub<PresenceHub>("/hubs/presence");

// Apply migrations automatically
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.Run();

public partial class Program { }
