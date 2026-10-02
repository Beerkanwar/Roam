using System;
using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Roam.Application.Sessions;

namespace Roam.Api.Endpoints;

public static class SessionEndpoints
{
    public static void MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ice-servers")
            .RequireAuthorization()
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
    }
}
