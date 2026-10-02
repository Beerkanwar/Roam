using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace Roam.Api.Hubs;

[Authorize]
public class PresenceHub : Hub<IPresenceClient>
{
    // Clients can subscribe to specific regions or places if needed
    public async Task JoinPlaceGroup(string placeId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"place_{placeId}");
    }

    public async Task LeavePlaceGroup(string placeId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"place_{placeId}");
    }
}
