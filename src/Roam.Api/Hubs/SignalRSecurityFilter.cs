using System;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Roam.Domain.Users;
using Roam.Infrastructure.Persistence;

namespace Roam.Api.Hubs;

public class SignalRSecurityFilter : IHubFilter
{
    private static readonly ConcurrentDictionary<string, (int Count, long WindowStart)> RateLimits = new();
    private const int MaxCallsPerWindow = 60; // 60 calls per minute
    private const long WindowTicks = 60 * TimeSpan.TicksPerSecond;

    private readonly IServiceProvider _serviceProvider;
    private readonly IMemoryCache _cache;

    public SignalRSecurityFilter(IServiceProvider serviceProvider, IMemoryCache cache)
    {
        _serviceProvider = serviceProvider;
        _cache = cache;
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext, 
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var userId = invocationContext.Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        // 1. Invocations rate limiting per connection / user
        var rateKey = userId ?? invocationContext.Context.ConnectionId;
        var nowTicks = DateTime.UtcNow.Ticks;

        RateLimits.AddOrUpdate(rateKey, 
            _ => (1, nowTicks), 
            (_, existing) =>
            {
                if (nowTicks - existing.WindowStart > WindowTicks)
                {
                    return (1, nowTicks);
                }
                return (existing.Count + 1, existing.WindowStart);
            });

        if (RateLimits.TryGetValue(rateKey, out var current) && current.Count > MaxCallsPerWindow)
        {
            throw new HubException("Too many requests. Please slow down.");
        }

        // 2. Real-time active account status verification
        if (!string.IsNullOrEmpty(userId))
        {
            var cacheKey = $"account_status_{userId}";
            if (!_cache.TryGetValue(cacheKey, out AccountStatus? accountStatus))
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                accountStatus = await dbContext.Users
                    .Where(u => u.Id == userId)
                    .Select(u => (AccountStatus?)u.AccountStatus)
                    .FirstOrDefaultAsync();

                _cache.Set(cacheKey, accountStatus, TimeSpan.FromSeconds(15));
            }

            if (accountStatus == AccountStatus.Suspended || 
                accountStatus == AccountStatus.Banned ||
                accountStatus == AccountStatus.PendingDeletion ||
                accountStatus == AccountStatus.Deleted)
            {
                invocationContext.Context.Abort();
                throw new HubException("Your account has been deactivated or suspended.");
            }
        }

        return await next(invocationContext);
    }
}
