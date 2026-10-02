using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Roam.Application.Chat;
using Roam.Contracts.Chat;

namespace Roam.Api.Endpoints;

public static class ChatEndpoints
{
    public static void MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/sessions")
            .RequireAuthorization()
            .WithTags("Chat")
            .WithApiVersionSet(app.NewApiVersionSet().Build())
            .HasApiVersion(1, 0);

        group.MapGet("/{sessionId}/chat", async (string sessionId, ClaimsPrincipal user, IChatService chatService, CancellationToken cancellationToken) =>
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Results.Unauthorized();
            }

            try
            {
                var history = await chatService.GetSessionChatHistoryAsync(sessionId, userId, cancellationToken);
                return Results.Ok(history);
            }
            catch (System.UnauthorizedAccessException)
            {
                return Results.Forbid();
            }
        });
    }
}
