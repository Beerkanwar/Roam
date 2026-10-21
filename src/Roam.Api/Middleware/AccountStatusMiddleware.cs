using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Roam.Domain.Users;
using Roam.Infrastructure.Persistence;

namespace Roam.Api.Middleware;

public class AccountStatusMiddleware
{
    private readonly RequestDelegate _next;

    public AccountStatusMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                var accountStatus = await dbContext.Users
                    .Where(u => u.Id == userId)
                    .Select(u => (AccountStatus?)u.AccountStatus)
                    .FirstOrDefaultAsync();

                if (accountStatus == AccountStatus.Suspended || accountStatus == AccountStatus.Banned)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/problem+json";
                    await context.Response.WriteAsJsonAsync(new
                    {
                        type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                        title = "Forbidden",
                        status = StatusCodes.Status403Forbidden,
                        detail = "Your account is currently suspended or banned."
                    });
                    return;
                }
            }
        }

        await _next(context);
    }
}
