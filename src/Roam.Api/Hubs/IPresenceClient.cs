using System.Threading.Tasks;

namespace Roam.Api.Hubs;

public interface IPresenceClient
{
    Task VolunteerAvailabilityChanged(string volunteerId, bool isAvailable);
    Task PlaceAvailabilityChanged(string placeId, bool isAvailable);
}
