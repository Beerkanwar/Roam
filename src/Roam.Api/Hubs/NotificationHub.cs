using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Roam.Api.Hubs;

[Authorize]
public class NotificationHub : Hub<INotificationClient>
{
    public async Task JoinVolunteerGroup()
    {
        if (Context.User?.HasClaim("VolunteerStatus", "Approved") == true || Context.User?.IsInRole("Admin") == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "volunteers");
        }
    }

    public async Task LeaveVolunteerGroup()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "volunteers");
    }
}
