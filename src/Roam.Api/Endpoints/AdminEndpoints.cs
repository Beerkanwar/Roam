using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Roam.Api.Security;
using Roam.Domain.TrustAndSafety;
using Roam.Domain.Users;
using Roam.Infrastructure.Persistence;

namespace Roam.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("/api/v1/admin")
            .WithTags("Admin")
            .RequireAuthorization("RequireAdminRole")
            .RequireRateLimiting("UserPolicy");

        // List pending volunteer applications
        group.MapGet("/volunteers/pending", async (ApplicationDbContext context, CancellationToken cancellationToken) =>
        {
            var pending = await context.Users
                .AsNoTracking()
                .Where(u => u.VolunteerStatus == VolunteerStatus.Pending)
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.DisplayName,
                    u.Country,
                    u.DateOfBirth,
                    VolunteerStatus = u.VolunteerStatus.ToString(),
                    u.CreatedAt
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(pending);
        });

        // Open safety reports, newest and most severe first, with both parties' names for the moderator.
        group.MapGet("/reports", async (ApplicationDbContext context, CancellationToken cancellationToken) =>
        {
            var open = new[] { ReportStatus.Pending, ReportStatus.UnderReview };
            var reports = await context.Reports
                .AsNoTracking()
                .Where(r => open.Contains(r.Status))
                .OrderByDescending(r => r.Severity)
                .ThenByDescending(r => r.CreatedAt)
                .Take(100)
                .Select(r => new
                {
                    r.Id,
                    r.TourSessionId,
                    Reason = r.Reason.ToString(),
                    Severity = r.Severity.ToString(),
                    Status = r.Status.ToString(),
                    r.Description,
                    r.CreatedAt,
                    r.ReporterId,
                    ReporterName = context.Users.Where(u => u.Id == r.ReporterId).Select(u => u.DisplayName).FirstOrDefault(),
                    ReporterEmail = context.Users.Where(u => u.Id == r.ReporterId).Select(u => u.Email).FirstOrDefault(),
                    r.ReportedUserId,
                    ReportedUserName = context.Users.Where(u => u.Id == r.ReportedUserId).Select(u => u.DisplayName).FirstOrDefault(),
                    ReportedUserEmail = context.Users.Where(u => u.Id == r.ReportedUserId).Select(u => u.Email).FirstOrDefault(),
                    ReportedUserStatus = context.Users.Where(u => u.Id == r.ReportedUserId).Select(u => u.AccountStatus.ToString()).FirstOrDefault()
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(reports);
        });

        // Approve a volunteer
        group.MapPost("/volunteers/{userId}/approve", async (string userId, UserManager<ApplicationUser> userManager, IUserStateCache userStateCache) =>
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user == null) return Results.NotFound(new { error = "User not found." });

            // Safeguarding: volunteers host live sessions, so only active, age-verified adults may be approved.
            if (user.AgeCategory != AgeCategory.Adult)
                return Results.BadRequest(new { error = "Only age-verified adult users (18+) can be approved as volunteers." });

            if (user.AccountStatus != AccountStatus.Active)
                return Results.BadRequest(new { error = "Only active accounts can be approved as volunteers." });

            user.VolunteerStatus = VolunteerStatus.Approved;
            user.UpdatedAt = DateTime.UtcNow;
            // Do NOT rotate the security stamp: that invalidates the user's refresh token and silently logs
            // them out. Claims are refreshed per request by UserStateClaimsTransformation instead.
            var result = await userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return Results.Problem("Failed to update volunteer status.", statusCode: StatusCodes.Status500InternalServerError);
            userStateCache.Invalidate(user.Id);

            return Results.Ok(new { message = $"Volunteer {user.Email} successfully approved.", userId = user.Id, status = "Approved" });
        });

        // Reject or revoke a volunteer
        group.MapPost("/volunteers/{userId}/reject", async (string userId, UserManager<ApplicationUser> userManager, IUserStateCache userStateCache) =>
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user == null) return Results.NotFound(new { error = "User not found." });

            user.VolunteerStatus = VolunteerStatus.Revoked;
            user.UpdatedAt = DateTime.UtcNow;
            var result = await userManager.UpdateAsync(user);
            if (!result.Succeeded)
                return Results.Problem("Failed to update volunteer status.", statusCode: StatusCodes.Status500InternalServerError);
            userStateCache.Invalidate(user.Id);

            return Results.Ok(new { message = $"Volunteer {user.Email} status updated to Revoked.", userId = user.Id, status = "Revoked" });
        });
    }
}
