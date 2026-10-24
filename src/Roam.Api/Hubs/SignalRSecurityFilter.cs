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
    private const int MaxCallsPerWindow = 180; // 180 calls per minute (accommodates WebRTC ICE bursts)
    private readonly IServiceProvider _serviceProvider;
    private readonly IMemoryCache _cache;

    public SignalRSecurityFilter(IServiceProvider serviceProvider, IMemoryCache cache)
    {
        _serviceProvider = serviceProvider;
        _cache = cache;
    }

    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        var userId = context.Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userId))
        {
            var status = await GetAccountStatusAsync(userId);
            if (IsDeactivated(status))
            {
                context.Context.Abort();
                throw new HubException("Your account has been deactivated or suspended.");
            }
        }

        await next(context);
    }

    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext, 
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var userId = invocationContext.Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        // 1. Invocations rate limiting per connection / user with automatic IMemoryCache eviction
        var rateKey = $"hub_rate_{userId ?? invocationContext.Context.ConnectionId}";
        var callCount = _cache.GetOrCreate(rateKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
            return 0;
        });

        callCount++;
        _cache.Set(rateKey, callCount, TimeSpan.FromMinutes(1));

        if (callCount > MaxCallsPerWindow)
        {
            throw new HubException("Too many requests. Please slow down.");
        }

        // 2. Real-time active account status verification
        if (!string.IsNullOrEmpty(userId))
        {
            var accountStatus = await GetAccountStatusAsync(userId);
            if (IsDeactivated(accountStatus))
            {
                invocationContext.Context.Abort();
                throw new HubException("Your account has been deactivated or suspended.");
            }
        }

        return await next(invocationContext);
    }

    private async Task<AccountStatus?> GetAccountStatusAsync(string userId)
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

        return accountStatus;
    }

    private static bool IsDeactivated(AccountStatus? status)
    {
        return status == AccountStatus.Suspended || 
               status == AccountStatus.Banned ||
               status == AccountStatus.PendingDeletion ||
               status == AccountStatus.Deleted;
    }
}
