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
            .WithTags("UserProfile")
            .RequireRateLimiting("UserPolicy");

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

            // 18+ Safeguarding check with realistic bounds
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (dto.DateOfBirth > today || dto.DateOfBirth < today.AddYears(-120))
            {
                return Results.BadRequest(new { error = "Please provide a valid date of birth." });
            }

            var age = today.Year - dto.DateOfBirth.Year;
            if (dto.DateOfBirth > today.AddYears(-age)) age--;

            if (age < 18)
            {
                return Results.BadRequest(new { error = "You must be at least 18 years old to use Roam." });
            }

            var appUser = await context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (appUser == null) return Results.NotFound();

            if (appUser.DateOfBirth.HasValue && appUser.DateOfBirth.Value != dto.DateOfBirth)
            {
                return Results.BadRequest(new { error = "Date of birth cannot be modified once verified." });
            }

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

        // GDPR Data Subject: Export My Data (M9: includes chat, reports, tips, location, blocks)
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

            var chatMessages = await context.ChatMessages
                .Where(m => m.SenderId == userId)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var reports = await context.Reports
                .Where(r => r.ReporterId == userId || r.ReportedUserId == userId)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var tips = await context.TipTransactions
                .Where(t => t.SenderId == userId || t.ReceiverId == userId)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var userLoc = await context.UserLocations
                .Where(l => l.UserId == userId)
                .Select(l => new { Latitude = l.Location.Y, Longitude = l.Location.X, l.LastUpdatedAt })
                .AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);

            var blocks = await context.UserBlocks
                .Where(b => b.BlockerId == userId)
                .Select(b => new { b.BlockedId, b.CreatedAt })
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
                    appUser.AgeCategory,
                    appUser.CreatedAt,
                    appUser.VolunteerStatus,
                    appUser.VolunteerSettings
                },
                TourRequests = tourRequests,
                Feedbacks = feedbacks,
                ChatMessages = chatMessages,
                Reports = reports,
                Tips = tips,
                Location = userLoc,
                Blocks = blocks
            };

            return Results.Ok(exportData);
        })
        .RequireAuthorization();

        // GDPR Data Subject: Delete / Anonymize Account (M8: anonymize chat, requests, feedback, blocks)
        group.MapDelete("/me", async (ClaimsPrincipal user, [FromBody] DeleteAccountDto? dto, UserManager<ApplicationUser> userManager, ApplicationDbContext context, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var appUser = await context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (appUser == null) return Results.NotFound();

            if (!string.IsNullOrEmpty(dto?.Password))
            {
                var isPasswordValid = await userManager.CheckPasswordAsync(appUser, dto.Password);
                if (!isPasswordValid)
                {
                    return Results.BadRequest(new { error = "Invalid password for account deletion." });
                }
            }

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

            // Anonymize user content across chat and feedback
            await context.ChatMessages
                .Where(m => m.SenderId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.Text, "[Message deleted due to account removal]"), cancellationToken);

            await context.Feedbacks
                .Where(f => f.ReviewerId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.Comments, "[Feedback author deleted]"), cancellationToken);

            await context.TourRequests
                .Where(r => r.RequesterId == userId && (r.Status == Roam.Domain.TourRequests.TourRequestStatus.Draft || r.Status == Roam.Domain.TourRequests.TourRequestStatus.Published))
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, Roam.Domain.TourRequests.TourRequestStatus.Cancelled)
                                          .SetProperty(r => r.CancellationReason, "User account deleted"), cancellationToken);

            // Remove location record
            var userLoc = await context.UserLocations.FirstOrDefaultAsync(l => l.UserId == userId, cancellationToken);
            if (userLoc != null) context.UserLocations.Remove(userLoc);

            // Remove volunteer settings
            var volSettings = await context.VolunteerSettings.FirstOrDefaultAsync(v => v.UserId == userId, cancellationToken);
            if (volSettings != null) context.VolunteerSettings.Remove(volSettings);

            // Remove blocks
            var userBlocks = await context.UserBlocks
                .Where(b => b.BlockerId == userId || b.BlockedId == userId)
                .ToListAsync(cancellationToken);
            if (userBlocks.Count > 0)
            {
                context.UserBlocks.RemoveRange(userBlocks);
            }

            await context.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization();

        // Apply to become a volunteer
        group.MapPost("/volunteer/apply", async (ClaimsPrincipal user, ApplicationDbContext context, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var appUser = await context.Users.FindAsync(new object[] { userId }, cancellationToken);
            if (appUser == null) return Results.NotFound();

            if (appUser.AgeCategory != AgeCategory.Adult)
                return Results.Problem("Only adult users (18+) can apply to be volunteers.", statusCode: StatusCodes.Status403Forbidden);

            if (appUser.VolunteerStatus == VolunteerStatus.Revoked)
                return Results.Problem("Your volunteer privileges have been revoked by platform administration. Please contact support.", statusCode: StatusCodes.Status403Forbidden);

            if (appUser.VolunteerStatus == VolunteerStatus.Approved)
                return Results.Ok(new { message = "Already approved as a volunteer." });

            appUser.VolunteerStatus = VolunteerStatus.Pending;
            appUser.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);

            return Results.Ok(new { message = "Volunteer application submitted. Awaiting administrative review." });
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

public class DeleteAccountDto
{
    public string? Password { get; set; }
}
