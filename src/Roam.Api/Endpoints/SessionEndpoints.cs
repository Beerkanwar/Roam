using System;
using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Roam.Application.Sessions;

namespace Roam.Api.Endpoints;

public static class SessionEndpoints
{
    public static void MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ice-servers")
            .RequireAuthorization()
            .RequireRateLimiting("UserPolicy")
            .WithTags("Sessions")
            .WithApiVersionSet(app.NewApiVersionSet().Build())
            .HasApiVersion(1, 0);

        group.MapGet("/", async (ClaimsPrincipal user, ITurnService turnService, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            var servers = await turnService.GetIceServersAsync(userId, cancellationToken);
            return Results.Ok(servers);
        });

        // End a live tour from inside the session (clients only know the session id there). Completes the
        // tour request, ends the session and notifies the other participant, so both can leave feedback.
        app.MapPost("/api/v1/sessions/{sessionId}/end", async (
            string sessionId,
            ClaimsPrincipal user,
            Roam.Infrastructure.Persistence.ApplicationDbContext context,
            Roam.Application.TourRequests.ITourRequestService tourRequestService,
            CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var session = await context.TourSessions
                .AsNoTracking()
                .Where(s => s.Id == sessionId)
                .Select(s => new { s.TourRequestId, s.Status })
                .FirstOrDefaultAsync(cancellationToken);
            if (session == null) return Results.NotFound();

            // Ending twice (both participants press "End tour") is not an error.
            if (session.Status is Roam.Domain.TourSessions.SessionStatus.Ended)
                return Results.Ok(new { ended = true });

            try
            {
                var completed = await tourRequestService.CompleteTourAsync(session.TourRequestId, userId, cancellationToken);
                return completed ? Results.Ok(new { ended = true }) : Results.NotFound();
            }
            catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
        })
        .RequireAuthorization()
        .RequireRateLimiting("UserPolicy")
        .WithTags("Sessions");
    }
}
