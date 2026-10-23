using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Roam.Domain.Users;
using Roam.Infrastructure.Persistence;

namespace Roam.Api.Middleware;

public class AccountStatusMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;

    public AccountStatusMiddleware(RequestDelegate next, IMemoryCache cache)
    {
        _next = next;
        _cache = cache;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                var cacheKey = $"account_status_{userId}";
                if (!_cache.TryGetValue(cacheKey, out AccountStatus? accountStatus))
                {
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
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/problem+json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                        title = "Forbidden",
                        status = StatusCodes.Status403Forbidden,
                        detail = "Your account is not active or has been marked for deletion."
                    });
                    return;
                }
            }
        }

        await _next(context);
    }
}
