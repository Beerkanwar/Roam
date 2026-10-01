using Asp.Versioning;
using Microsoft.EntityFrameworkCore;
using Roam.Infrastructure.Persistence;
using Roam.Application.Places;
using Roam.Infrastructure.Places;
using Roam.Infrastructure.Maps;
using Roam.Api.Endpoints;
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

// Add Places Services
builder.Services.AddHttpClient<IGeocodingService, OsmGeocodingService>();
builder.Services.AddScoped<IPlaceService, PlaceService>();

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

// Build app
var app = builder.Build();

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment()) 
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Map Health Checks
app.MapHealthChecks("/health");

app.MapPlacesEndpoints();

app.MapGet("/", () => "Roam API is running").WithApiVersionSet(app.NewApiVersionSet().Build());

app.Run();
