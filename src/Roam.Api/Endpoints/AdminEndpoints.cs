using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Roam.Domain.Users;
using Roam.Infrastructure.Persistence;

namespace Roam.Api.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("/api/v1/admin")
            .WithTags("Admin")
            .RequireAuthorization("RequireAdminRole");

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

        // Approve a volunteer
        group.MapPost("/volunteers/{userId}/approve", async (string userId, UserManager<ApplicationUser> userManager) =>
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user == null) return Results.NotFound(new { error = "User not found." });

            user.VolunteerStatus = VolunteerStatus.Approved;
            user.UpdatedAt = DateTime.UtcNow;
            await userManager.UpdateSecurityStampAsync(user);
            await userManager.UpdateAsync(user);

            return Results.Ok(new { message = $"Volunteer {user.Email} successfully approved.", userId = user.Id, status = "Approved" });
        });

        // Reject or revoke a volunteer
        group.MapPost("/volunteers/{userId}/reject", async (string userId, UserManager<ApplicationUser> userManager) =>
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user == null) return Results.NotFound(new { error = "User not found." });

            user.VolunteerStatus = VolunteerStatus.Revoked;
            user.UpdatedAt = DateTime.UtcNow;
            await userManager.UpdateSecurityStampAsync(user);
            await userManager.UpdateAsync(user);

            return Results.Ok(new { message = $"Volunteer {user.Email} status updated to Revoked.", userId = user.Id, status = "Revoked" });
        });
    }
}
