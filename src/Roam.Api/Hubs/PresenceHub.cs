using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Roam.Domain.TourRequests;
using Roam.Infrastructure.Persistence;

namespace Roam.Api.Hubs;

[Authorize]
public class PresenceHub : Hub<IPresenceClient>
{
    private readonly ApplicationDbContext _context;

    public PresenceHub(ApplicationDbContext context)
    {
        _context = context;
    }

    // Clients can subscribe to specific regions or places if authorized
    public async Task JoinPlaceGroup(string placeId)
    {
        if (string.IsNullOrWhiteSpace(placeId))
            throw new HubException("Invalid place ID.");

        var user = Context.User;
        var userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            throw new HubException("Unauthorized.");

        var placeExists = await _context.Places.AnyAsync(p => p.Id == placeId);
        if (!placeExists)
            throw new HubException("Place not found.");

        var isAdmin = user?.IsInRole("Admin") == true;
        var isVolunteer = user?.HasClaim("VolunteerStatus", "Approved") == true;

        if (!isAdmin && !isVolunteer)
        {
            var hasActiveRequestAtPlace = await _context.TourRequests.AnyAsync(r =>
                r.PlaceId == placeId &&
                r.RequesterId == userId &&
                r.Status != TourRequestStatus.Expired &&
                r.Status != TourRequestStatus.Cancelled);

            if (!hasActiveRequestAtPlace)
            {
                throw new HubException("User is not authorized to join this place group.");
            }
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"place_{placeId}");
    }

    public async Task LeavePlaceGroup(string placeId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"place_{placeId}");
    }
}
