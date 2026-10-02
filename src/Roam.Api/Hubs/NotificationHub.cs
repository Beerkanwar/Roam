using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Roam.Api.Hubs;

[Authorize]
public class NotificationHub : Hub<INotificationClient>
{
}
