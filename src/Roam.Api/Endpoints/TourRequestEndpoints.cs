using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Roam.Application.TourRequests;
using Roam.Domain.TourSessions;
using Roam.Infrastructure.Persistence;

namespace Roam.Api.Endpoints;

public static class TourRequestEndpoints
{
    public static void MapTourRequestEndpoints(this IEndpointRouteBuilder builder)
    {
        var group = builder.MapGroup("/api/v1/tour-requests")
            .WithTags("TourRequests")
            .RequireAuthorization()
            .RequireRateLimiting("UserPolicy");

        group.MapPost("/", async (ClaimsPrincipal user, [FromBody] CreateTourRequestDto dto, ITourRequestService service, ApplicationDbContext context, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var appUser = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (appUser == null || appUser.AgeCategory != Roam.Domain.Users.AgeCategory.Adult)
            {
                return Results.Problem("Only age-verified adult users (18+) can create tour requests.", statusCode: StatusCodes.Status403Forbidden);
            }

            if (string.IsNullOrWhiteSpace(dto.Description) || dto.Description.Length > 2000)
                return Results.BadRequest(new { error = "Description must be between 1 and 2000 characters." });

            dto.RequesterId = userId;
            try
            {
                var request = await service.CreateRequestAsync(dto, cancellationToken);
                return Results.Created($"/api/v1/tour-requests/{request.Id}", ToDto(request));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/coordinates", async (ClaimsPrincipal user, [FromBody] CreateTourRequestByCoordinatesDto dto, ITourRequestService service, ApplicationDbContext context, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            var appUser = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (appUser == null || appUser.AgeCategory != Roam.Domain.Users.AgeCategory.Adult)
            {
                return Results.Problem("Only age-verified adult users (18+) can create tour requests.", statusCode: StatusCodes.Status403Forbidden);
            }

            if (dto.Latitude is < -90 or > 90)
                return Results.BadRequest(new { error = "Latitude must be between -90 and 90." });

            if (dto.Longitude is < -180 or > 180)
                return Results.BadRequest(new { error = "Longitude must be between -180 and 180." });

            if (string.IsNullOrWhiteSpace(dto.Description) || dto.Description.Length > 2000)
                return Results.BadRequest(new { error = "Description must be between 1 and 2000 characters." });

            dto.RequesterId = userId;
            try
            {
                var request = await service.CreateRequestByCoordinatesAsync(dto, cancellationToken);
                return Results.Created($"/api/v1/tour-requests/{request.Id}", ToDto(request));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        group.MapPost("/{id}/accept", async (string id, ClaimsPrincipal user, ITourRequestService service, CancellationToken cancellationToken) =>
        {
            var volunteerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(volunteerId)) return Results.Unauthorized();

            try
            {
                var success = await service.AcceptRequestAsync(id, volunteerId, cancellationToken);
                return success ? Results.Ok() : Results.NotFound();
            }
            catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
        });

        group.MapPost("/{id}/schedule/propose", async (string id, [FromBody] ProposeScheduleDto dto, ClaimsPrincipal user, ITourRequestService service, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            try
            {
                var schedule = await service.ProposeScheduleAsync(id, userId, dto.ProposedStartUtc, dto.ProposedEndUtc, cancellationToken);
                return Results.Ok(schedule);
            }
            catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        group.MapPost("/schedule/{scheduleId}/accept", async (string scheduleId, ClaimsPrincipal user, ITourRequestService service, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            try
            {
                var success = await service.AcceptScheduleAsync(scheduleId, userId, cancellationToken);
                return success ? Results.Ok() : Results.BadRequest("Cannot accept schedule.");
            }
            catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        group.MapPost("/schedule/{scheduleId}/reject", async (string scheduleId, ClaimsPrincipal user, ITourRequestService service, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            try
            {
                var success = await service.RejectScheduleAsync(scheduleId, userId, cancellationToken);
                return success ? Results.Ok() : Results.BadRequest("Cannot reject schedule.");
            }
            catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        group.MapGet("/", async (ClaimsPrincipal user, ITourRequestService service, [FromQuery] double radius = 5000, CancellationToken cancellationToken = default) =>
        {
            var volunteerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(volunteerId)) return Results.Unauthorized();

            try
            {
                var requests = await service.GetNearbyRequestsAsync(volunteerId, radius, cancellationToken);
                
                // Return safe DTO without RequesterId to volunteer view
                var response = requests.Select(r => new
                {
                    r.Id,
                    r.Description,
                    r.Mode,
                    r.Visibility,
                    Place = r.Place != null ? new 
                    {
                        r.Place.Id,
                        r.Place.Name,
                        r.Place.Description,
                        Latitude = r.Place.Location.Y,
                        Longitude = r.Place.Location.X
                    } : null
                });

                return Results.Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .RequireAuthorization("RequireApprovedVolunteer");

        group.MapPost("/{id}/cancel", async (string id, [FromBody] CancelRequestDto dto, ClaimsPrincipal user, ITourRequestService service, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            try
            {
                var success = await service.CancelRequestAsync(id, userId, dto.Reason, cancellationToken);
                return success ? Results.Ok() : Results.NotFound();
            }
            catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        group.MapPost("/{id}/start", async (string id, ClaimsPrincipal user, ITourRequestService service, ApplicationDbContext context, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            try
            {
                var success = await service.StartTourAsync(id, userId, cancellationToken);
                if (!success) return Results.NotFound();

                var session = await context.TourSessions
                    .Where(s => s.TourRequestId == id && s.Status == SessionStatus.Active)
                    .OrderByDescending(s => s.StartedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                return Results.Ok(new { success = true, sessionId = session?.Id });
            }
            catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        group.MapPost("/{id}/complete", async (string id, ClaimsPrincipal user, ITourRequestService service, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            try
            {
                var success = await service.CompleteTourAsync(id, userId, cancellationToken);
                return success ? Results.Ok() : Results.NotFound();
            }
            catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        group.MapPost("/{id}/interest", async (string id, ClaimsPrincipal user, ITourRequestService service, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

            try
            {
                var success = await service.MarkVolunteerInterestedAsync(id, userId, cancellationToken);
                return success ? Results.Ok() : Results.NotFound();
            }
            catch (UnauthorizedAccessException ex) { return Results.Problem(ex.Message, statusCode: StatusCodes.Status403Forbidden); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });
    }

    private static TourRequestResponseDto ToDto(Roam.Domain.TourRequests.TourRequest r) => new()
    {
        Id = r.Id,
        RequesterId = r.RequesterId,
        PlaceId = r.PlaceId,
        Status = (int)r.Status,
        StatusName = r.Status.ToString(),
        Visibility = (int)r.Visibility,
        Mode = (int)r.Mode,
        Description = r.Description,
        CreatedAt = r.CreatedAt,
        ExpiresAt = r.ExpiresAt,
        AssignedVolunteerId = r.AssignedVolunteerId
    };
}

public class CancelRequestDto
{
    public string Reason { get; set; } = string.Empty;
}

public class ProposeScheduleDto
{
    public DateTime ProposedStartUtc { get; set; }
    public DateTime? ProposedEndUtc { get; set; }
}
