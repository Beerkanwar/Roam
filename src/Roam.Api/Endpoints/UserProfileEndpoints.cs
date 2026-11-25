using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Roam.Api.Security;
using Roam.Application.TourRequests;
using Roam.Domain.Places;
using Roam.Domain.Users;
using Roam.Infrastructure.Persistence;

namespace Roam.Api.Endpoints;

public static class UserProfileEndpoints
{
    internal const int MaxProfilePhotoUrlLength = 2048;
    internal const int MaxLanguagesLength = 200;

    /// <summary>
    /// A profile photo URL is optional; when present it must be an absolute https URL so clients
    /// never render script (javascript:), inline (data:) or plaintext (http:) sources.
    /// </summary>
    internal static bool IsValidProfilePhotoUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return true;
        var trimmed = url.Trim();
        if (trimmed.Length > MaxProfilePhotoUrlLength) return false;
        return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
               && uri.Scheme == Uri.UriSchemeHttps
               && !string.IsNullOrEmpty(uri.Host)
               && string.IsNullOrEmpty(uri.UserInfo);
    }

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

        // Current user authenticated profile
        group.MapGet("/me", async (ClaimsPrincipal user, ApplicationDbContext context, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var appUser = await context.Users
                .AsNoTracking()
                .Include(u => u.VolunteerSettings)
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

            if (appUser == null || appUser.AccountStatus != AccountStatus.Active)
                return Results.NotFound();

            return Results.Ok(new
            {
                appUser.Id,
                appUser.Email,
                appUser.EmailConfirmed,
                appUser.DisplayName,
                appUser.Country,
                appUser.ProfilePhotoUrl,
                appUser.VolunteerStatus,
                appUser.AgeCategory,
                // Roles come from the live claims (UserStateClaimsTransformation), so this is current.
                IsAdmin = user.IsInRole("Admin"),
                VolunteerSettings = appUser.VolunteerSettings != null ? new
                {
                    appUser.VolunteerSettings.Enabled,
                    appUser.VolunteerSettings.AvailableForRequests,
                    appUser.VolunteerSettings.AcceptsPrivateTours,
                    appUser.VolunteerSettings.AcceptsGroupTours,
                    appUser.VolunteerSettings.MaxConcurrentViewers,
                    appUser.VolunteerSettings.Languages,
                    appUser.VolunteerSettings.TipEnabled
                } : null
            });
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

            if (!IsValidProfilePhotoUrl(dto.ProfilePhotoUrl))
                return Results.BadRequest(new { error = $"Profile photo URL must be an absolute https URL of at most {MaxProfilePhotoUrlLength} characters." });

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
            appUser.ProfilePhotoUrl = string.IsNullOrWhiteSpace(dto.ProfilePhotoUrl) ? null : dto.ProfilePhotoUrl.Trim();
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

            if (dto.Languages?.Length > MaxLanguagesLength)
                return Results.BadRequest(new { error = $"Languages cannot exceed {MaxLanguagesLength} characters." });

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
                .Select(r => new
                {
                    r.Id,
                    r.RequesterId,
                    r.AssignedVolunteerId,
                    r.PlaceId,
                    r.Visibility,
                    r.Mode,
                    r.Status,
                    r.Description,
                    r.RequestedStartTime,
                    r.CreatedAt,
                    r.ExpiresAt,
                    r.CancelledByUserId,
                    r.CancellationReason,
                    r.CancelledAt
                })
                .ToListAsync(cancellationToken);

            var feedbacks = await context.Feedbacks
                .Where(f => f.ReviewerId == userId || f.TargetUserId == userId)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var chatMessages = await context.ChatMessages
                .Where(m => m.SenderId == userId)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            // Reports filed BY the user are exported in full. Reports filed AGAINST the user are exported
            // without the reporter's identity or free-text description, so the export cannot be used to
            // identify (and retaliate against) whoever reported them.
            var reportsFiled = await context.Reports
                .Where(r => r.ReporterId == userId)
                .AsNoTracking()
                .Select(r => new
                {
                    r.Id,
                    r.ReportedUserId,
                    r.TourSessionId,
                    r.Reason,
                    r.Description,
                    r.Severity,
                    r.Status,
                    r.CreatedAt,
                    r.ResolvedAt
                })
                .ToListAsync(cancellationToken);

            var reportsReceived = await context.Reports
                .Where(r => r.ReportedUserId == userId && r.ReporterId != userId)
                .AsNoTracking()
                .Select(r => new
                {
                    r.Id,
                    r.TourSessionId,
                    r.Reason,
                    r.Status,
                    r.CreatedAt,
                    r.ResolvedAt
                })
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
                    // Explicit projection: serializing the entity would follow VolunteerSettings.User back to
                    // ApplicationUser (object cycle -> 500, and exposure of PasswordHash/SecurityStamp).
                    VolunteerSettings = appUser.VolunteerSettings == null ? null : new
                    {
                        appUser.VolunteerSettings.Enabled,
                        appUser.VolunteerSettings.AvailableForRequests,
                        appUser.VolunteerSettings.AcceptsPrivateTours,
                        appUser.VolunteerSettings.AcceptsGroupTours,
                        appUser.VolunteerSettings.MaxConcurrentViewers,
                        appUser.VolunteerSettings.Languages,
                        appUser.VolunteerSettings.TipEnabled
                    }
                },
                TourRequests = tourRequests,
                Feedbacks = feedbacks,
                ChatMessages = chatMessages,
                Reports = new { Filed = reportsFiled, Received = reportsReceived },
                Tips = tips,
                Location = userLoc,
                Blocks = blocks
            };

            return Results.Ok(exportData);
        })
        .RequireAuthorization();

        // GDPR Data Subject: Delete / Anonymize Account (M8: anonymize chat, requests, feedback, blocks)
        group.MapDelete("/me", async (ClaimsPrincipal user, [FromBody] DeleteAccountDto? dto, UserManager<ApplicationUser> userManager, ApplicationDbContext context,
            ITourRequestService tourRequestService, ITourNotificationService notificationService, IUserStateCache userStateCache, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var appUser = await context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (appUser == null) return Results.NotFound();

            if (string.IsNullOrWhiteSpace(dto?.Password))
            {
                return Results.BadRequest(new { error = "Password confirmation is required for account deletion." });
            }

            var isPasswordValid = await userManager.CheckPasswordAsync(appUser, dto.Password);
            if (!isPasswordValid)
            {
                return Results.BadRequest(new { error = "Invalid password for account deletion." });
            }

            // All anonymization steps must succeed or fail together: ExecuteUpdateAsync commits immediately,
            // so without a transaction a later failure would leave a half-anonymized account.
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

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

            // Cancel every request the user still has in flight (not only Draft/Published), so volunteers
            // are not left holding interested/accepted/confirmed tours for a deleted account.
            var cancellableStatuses = new[]
            {
                Roam.Domain.TourRequests.TourRequestStatus.Draft,
                Roam.Domain.TourRequests.TourRequestStatus.Published,
                Roam.Domain.TourRequests.TourRequestStatus.Matching,
                Roam.Domain.TourRequests.TourRequestStatus.VolunteerInterested,
                Roam.Domain.TourRequests.TourRequestStatus.Accepted,
                Roam.Domain.TourRequests.TourRequestStatus.Confirmed
            };
            var now = DateTime.UtcNow;
            await context.TourRequests
                .Where(r => r.RequesterId == userId && cancellableStatuses.Contains(r.Status))
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, Roam.Domain.TourRequests.TourRequestStatus.Cancelled)
                                          .SetProperty(r => r.CancelledByUserId, userId)
                                          .SetProperty(r => r.CancelledAt, now)
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

            // A deleted volunteer must not stay assigned: pre-start assignments go back to discovery and
            // live sessions they are in are ended (same transaction, same DbContext).
            var tourChanges = await tourRequestService.ReleaseToursForDeletedUserAsync(userId, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            userStateCache.Invalidate(userId);

            // Notify only after commit so clients never react to changes that were rolled back.
            foreach (var (requestId, requesterId) in tourChanges.RepublishedRequests)
            {
                await notificationService.VolunteerWithdrawnAsync(requestId, requesterId);
                await notificationService.TourRequestCreatedAsync(requestId);
            }
            foreach (var (sessionId, otherUserId) in tourChanges.EndedSessions)
            {
                await notificationService.SessionEndedForUserAsync(sessionId, otherUserId);
            }

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
