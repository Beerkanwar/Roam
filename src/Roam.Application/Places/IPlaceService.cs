using Roam.Domain.Places;

namespace Roam.Application.Places;

public interface IPlaceService
{
    Task<IEnumerable<Place>> SearchPlacesAsync(string query, CancellationToken cancellationToken = default);
    Task<Place?> GetPlaceByIdAsync(string id, CancellationToken cancellationToken = default);
}
