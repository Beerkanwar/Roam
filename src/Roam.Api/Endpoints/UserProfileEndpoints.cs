using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Roam.Domain.Places;
using Roam.Domain.Users;
using Roam.Infrastructure.Persistence;

namespace Roam.Api.Endpoints;

public static class UserProfileEndpoints
{
    public static void MapUserProfileEndpoints(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("/api/v1/profile")
            .WithTags("UserProfile");

        // Public profile lookup (minimal safe fields)
        group.MapGet("/{id}", async (string id, ApplicationDbContext context, CancellationToken cancellationToken) =>
        {
            var user = await context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

            if (user == null || user.AccountStatus != AccountStatus.Active)
                return Results.NotFound();

            var publicProfile = new
            {
                user.Id,
                user.DisplayName,
                user.Country,
                user.ProfilePhotoUrl
            };

            return Results.Ok(publicProfile);
        })
        .RequireAuthorization();

        // Onboarding endpoint with 18+ safeguarding verification
        group.MapPost("/onboarding", async (ClaimsPrincipal user, [FromBody] OnboardingDto dto, ApplicationDbContext context, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(dto.DisplayName) || dto.DisplayName.Length > 50)
                return Results.BadRequest(new { error = "Display name must be between 1 and 50 characters." });

            if (string.IsNullOrWhiteSpace(dto.Country) || dto.Country.Length > 100)
                return Results.BadRequest(new { error = "Valid country is required." });

            // 18+ Safeguarding check
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var age = today.Year - dto.DateOfBirth.Year;
            if (dto.DateOfBirth > today.AddYears(-age)) age--;

            if (age < 18)
            {
                return Results.BadRequest(new { error = "You must be at least 18 years old to use Roam." });
            }

            var appUser = await context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (appUser == null) return Results.NotFound();

            appUser.DisplayName = dto.DisplayName.Trim();
            appUser.Country = dto.Country.Trim();
            appUser.DateOfBirth = dto.DateOfBirth;
            appUser.AgeCategory = AgeCategory.Adult;
            appUser.ProfilePhotoUrl = dto.ProfilePhotoUrl;
            appUser.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync(cancellationToken);
            return Results.Ok(new { message = "Onboarding completed successfully." });
        })
        .RequireAuthorization();

        // Update Volunteer Settings
        group.MapPut("/volunteer-settings", async (ClaimsPrincipal user, [FromBody] UpdateVolunteerSettingsDto dto, ApplicationDbContext context, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            if (dto.MaxConcurrentViewers is < 1 or > 100)
                return Results.BadRequest(new { error = "Max concurrent viewers must be between 1 and 100." });

            var existing = await context.VolunteerSettings.FirstOrDefaultAsync(vs => vs.UserId == userId, cancellationToken);

            if (existing == null)
            {
                var newSettings = new VolunteerSettings
                {
                    UserId = userId,
                    Enabled = dto.Enabled,
                    AvailableForRequests = dto.AvailableForRequests,
                    AcceptsPrivateTours = dto.AcceptsPrivateTours,
                    AcceptsGroupTours = dto.AcceptsGroupTours,
                    MaxConcurrentViewers = dto.MaxConcurrentViewers,
                    Languages = dto.Languages ?? string.Empty,
                    TipEnabled = dto.TipEnabled
                };
                context.VolunteerSettings.Add(newSettings);
            }
            else
            {
                existing.Enabled = dto.Enabled;
                existing.AvailableForRequests = dto.AvailableForRequests;
                existing.AcceptsPrivateTours = dto.AcceptsPrivateTours;
                existing.AcceptsGroupTours = dto.AcceptsGroupTours;
                existing.MaxConcurrentViewers = dto.MaxConcurrentViewers;
                existing.Languages = dto.Languages ?? string.Empty;
                existing.TipEnabled = dto.TipEnabled;
            }

            await context.SaveChangesAsync(cancellationToken);
            return Results.Ok();
        })
        .RequireAuthorization();

        // Update Location with coordinate validation
        group.MapPut("/location", async (ClaimsPrincipal user, [FromBody] UpdateLocationDto dto, ApplicationDbContext context, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            // Validate coordinate ranges
            if (dto.Latitude is < -90 or > 90)
                return Results.BadRequest(new { error = "Latitude must be between -90 and 90." });

            if (dto.Longitude is < -180 or > 180)
                return Results.BadRequest(new { error = "Longitude must be between -180 and 180." });

            var location = new Point(dto.Longitude, dto.Latitude) { SRID = 4326 };

            var existing = await context.UserLocations.FirstOrDefaultAsync(l => l.UserId == userId, cancellationToken);

            if (existing == null)
            {
                var userLoc = new UserLocation 
                { 
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    Location = location,
                    LastUpdatedAt = DateTime.UtcNow
                };
                context.UserLocations.Add(userLoc);
            }
            else
            {
                existing.Location = location;
                existing.LastUpdatedAt = DateTime.UtcNow;
            }

            await context.SaveChangesAsync(cancellationToken);
            return Results.Ok();
        })
        .RequireAuthorization();

        // GDPR Data Subject: Export My Data
        group.MapGet("/me/export", async (ClaimsPrincipal user, ApplicationDbContext context, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var appUser = await context.Users
                .Include(u => u.VolunteerSettings)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

            if (appUser == null) return Results.NotFound();

            var tourRequests = await context.TourRequests
                .Where(r => r.RequesterId == userId || r.AssignedVolunteerId == userId)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var feedbacks = await context.Feedbacks
                .Where(f => f.ReviewerId == userId || f.TargetUserId == userId)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var exportData = new
            {
                Profile = new
                {
                    appUser.Id,
                    appUser.Email,
                    appUser.DisplayName,
                    appUser.Country,
                    appUser.DateOfBirth,
                    appUser.CreatedAt,
                    appUser.VolunteerStatus,
                    appUser.VolunteerSettings
                },
                TourRequests = tourRequests,
                Feedbacks = feedbacks
            };

            return Results.Ok(exportData);
        })
        .RequireAuthorization();

        // GDPR Data Subject: Delete / Anonymize Account
        group.MapDelete("/me", async (ClaimsPrincipal user, ApplicationDbContext context, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var appUser = await context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (appUser == null) return Results.NotFound();

            // Anonymize personal details
            appUser.DisplayName = "Deleted User";
            appUser.Email = $"deleted-{Guid.NewGuid()}@deleted.local";
            appUser.NormalizedEmail = appUser.Email.ToUpperInvariant();
            appUser.UserName = appUser.Email;
            appUser.NormalizedUserName = appUser.Email.ToUpperInvariant();
            appUser.PhoneNumber = null;
            appUser.Country = string.Empty;
            appUser.ProfilePhotoUrl = null;
            appUser.DateOfBirth = null;
            appUser.AccountStatus = AccountStatus.Deleted;
            appUser.SecurityStamp = Guid.NewGuid().ToString();

            // Remove location record
            var userLoc = await context.UserLocations.FirstOrDefaultAsync(l => l.UserId == userId, cancellationToken);
            if (userLoc != null) context.UserLocations.Remove(userLoc);

            // Remove volunteer settings
            var volSettings = await context.VolunteerSettings.FirstOrDefaultAsync(v => v.UserId == userId, cancellationToken);
            if (volSettings != null) context.VolunteerSettings.Remove(volSettings);

            await context.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization();
    }
}

public class OnboardingDto
{
    public string DisplayName { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string? ProfilePhotoUrl { get; set; }
}

public class UpdateVolunteerSettingsDto
{
    public bool Enabled { get; set; }
    public bool AvailableForRequests { get; set; }
    public bool AcceptsPrivateTours { get; set; }
    public bool AcceptsGroupTours { get; set; }
    public int MaxConcurrentViewers { get; set; } = 10;
    public string? Languages { get; set; }
    public bool TipEnabled { get; set; }
}

public class UpdateLocationDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
