using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Roam.Domain.Users;
using Roam.Infrastructure.Persistence;

namespace Roam.Api.Endpoints;

public static class UserProfileEndpoints
{
    public static void MapUserProfileEndpoints(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("/api/v1/profile")
            .WithTags("UserProfile")
            .WithApiVersionSet(builder.NewApiVersionSet().Build());

        group.MapGet("/{id}", async (string id, ApplicationDbContext context, CancellationToken cancellationToken) =>
        {
            var user = await context.Users
                .Include(u => u.VolunteerSettings)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

            if (user == null)
                return Results.NotFound();

            // Return safe public profile
            var publicProfile = new
            {
                user.Id,
                user.DisplayName,
                user.Country,
                user.ProfilePhotoUrl,
                user.VolunteerStatus,
                Languages = user.VolunteerSettings?.Languages,
                AcceptsPrivateTours = user.VolunteerSettings?.AcceptsPrivateTours,
                AcceptsGroupTours = user.VolunteerSettings?.AcceptsGroupTours
            };

            return Results.Ok(publicProfile);
        });

        group.MapPut("/volunteer-settings", async (ClaimsPrincipal user, [FromBody] VolunteerSettings dto, ApplicationDbContext context, CancellationToken cancellationToken) =>
        {
            dto.UserId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("User ID missing from token.");

            var existing = await context.VolunteerSettings.FirstOrDefaultAsync(vs => vs.UserId == dto.UserId, cancellationToken);

            if (existing == null)
            {
                context.VolunteerSettings.Add(dto);
            }
            else
            {
                existing.Enabled = dto.Enabled;
                existing.AvailableForRequests = dto.AvailableForRequests;
                existing.AcceptsPrivateTours = dto.AcceptsPrivateTours;
                existing.AcceptsGroupTours = dto.AcceptsGroupTours;
                existing.MaxConcurrentViewers = dto.MaxConcurrentViewers;
                existing.Languages = dto.Languages;
                existing.TipEnabled = dto.TipEnabled;
            }

            await context.SaveChangesAsync(cancellationToken);
            return Results.Ok();
        }).RequireAuthorization();
    }
}
