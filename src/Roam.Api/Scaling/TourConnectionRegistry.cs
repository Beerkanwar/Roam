using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace Roam.Api.Scaling;

/// <summary>Which tour session (and user) a SignalR connection joined.</summary>
public sealed record TourConnection(string SessionId, string UserId);

/// <summary>
/// Maps TourHub connection ids to the session they joined. Stored in the distributed cache (Redis when
/// configured) rather than in a static dictionary, so a client connected to server A can signal a peer
/// connected to server B: the target lookup must see connections registered on every server.
/// </summary>
public interface ITourConnectionRegistry
{
    Task RegisterAsync(string connectionId, TourConnection connection);
    Task<TourConnection?> GetAsync(string connectionId);
    Task RemoveAsync(string connectionId);
}

public sealed class DistributedTourConnectionRegistry : ITourConnectionRegistry
{
    // Sessions are force-ended after 4 hours; entries outlive that slightly, then expire on their own
    // even if the owning server crashed before OnDisconnectedAsync could remove them.
    private static readonly DistributedCacheEntryOptions EntryOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(5)
    };

    private readonly IDistributedCache _cache;

    public DistributedTourConnectionRegistry(IDistributedCache cache)
    {
        _cache = cache;
    }

    private static string Key(string connectionId) => $"tourhub:conn:{connectionId}";

    public Task RegisterAsync(string connectionId, TourConnection connection) =>
        _cache.SetAsync(Key(connectionId), JsonSerializer.SerializeToUtf8Bytes(connection), EntryOptions);

    public async Task<TourConnection?> GetAsync(string connectionId)
    {
        var bytes = await _cache.GetAsync(Key(connectionId));
        return bytes == null ? null : JsonSerializer.Deserialize<TourConnection>(bytes);
    }

    public Task RemoveAsync(string connectionId) => _cache.RemoveAsync(Key(connectionId));
}
