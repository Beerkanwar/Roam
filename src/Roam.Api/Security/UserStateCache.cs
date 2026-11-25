using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Roam.Domain.Users;
using Roam.Infrastructure.Persistence;
using StackExchange.Redis;

namespace Roam.Api.Security;

/// <summary>The authorization-relevant state of a user, read from the database.</summary>
public sealed record UserState(AccountStatus AccountStatus, VolunteerStatus VolunteerStatus, IReadOnlyList<string> Roles);

public interface IUserStateCache
{
    /// <summary>Current state of the user, or null if the user does not exist.</summary>
    Task<UserState?> GetAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Drops cached state on every server (via Redis when configured) so the next request re-reads the database.</summary>
    void Invalidate(string userId);
}

/// <summary>
/// Short-lived per-server cache of user state. Access tokens are long-lived bearer tickets, so their
/// embedded VolunteerStatus/AccountStatus/role claims go stale; reading the live state here lets
/// permissions change without rotating the security stamp, which would invalidate refresh tokens.
/// Invalidations are broadcast to all servers over Redis pub/sub; <see cref="Ttl"/> bounds staleness
/// if a broadcast is ever missed (e.g. during a Redis outage).
/// </summary>
public sealed class UserStateCache : IUserStateCache
{
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(15);

    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISubscriber? _subscriber;
    private readonly ILogger<UserStateCache>? _logger;

    internal static readonly RedisChannel InvalidationChannel = RedisChannel.Literal("roam:userstate:invalidate");

    public UserStateCache(IMemoryCache cache, IServiceScopeFactory scopeFactory,
        IConnectionMultiplexer? redis = null, ILogger<UserStateCache>? logger = null)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
        _logger = logger;

        if (redis is null) return;
        try
        {
            var subscriber = redis.GetSubscriber();
            subscriber.Subscribe(InvalidationChannel, (_, message) =>
            {
                if (!message.IsNullOrEmpty) RemoveLocal(message!);
            });
            _subscriber = subscriber;
        }
        catch (Exception ex)
        {
            // Fall back to TTL-bounded staleness rather than failing authentication.
            _logger?.LogWarning(ex, "Could not subscribe to user-state invalidations; other servers may see changes up to {Ttl} late.", Ttl);
        }
    }

    private static string Key(string userId) => $"user_state_{userId}";

    public async Task<UserState?> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(Key(userId), out UserState? cached))
            return cached;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var user = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.AccountStatus, u.VolunteerStatus })
            .FirstOrDefaultAsync(cancellationToken);

        UserState? state = null;
        if (user != null)
        {
            var roles = await db.UserRoles
                .Where(ur => ur.UserId == userId)
                .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name!)
                .ToListAsync(cancellationToken);
            state = new UserState(user.AccountStatus, user.VolunteerStatus, roles);
        }

        _cache.Set(Key(userId), state, Ttl);
        return state;
    }

    public void Invalidate(string userId)
    {
        RemoveLocal(userId);
        if (_subscriber is null) return;
        try
        {
            _subscriber.Publish(InvalidationChannel, userId, CommandFlags.FireAndForget);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Could not broadcast user-state invalidation for {UserId}.", userId);
        }
    }

    private void RemoveLocal(string userId)
    {
        _cache.Remove(Key(userId));
        // Also used by AccountStatusMiddleware and SignalRSecurityFilter.
        _cache.Remove($"account_status_{userId}");
    }
}
