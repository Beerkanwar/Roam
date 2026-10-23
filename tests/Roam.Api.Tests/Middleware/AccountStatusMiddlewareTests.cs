using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Roam.Api.Middleware;
using Roam.Domain.Users;
using Xunit;

namespace Roam.Api.Tests.Middleware;

public class AccountStatusMiddlewareTests
{
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

    [Fact]
    public async Task UnauthenticatedUser_PassesThroughToNext()
    {
        // Arrange
        bool nextInvoked = false;
        var middleware = new AccountStatusMiddleware(_ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        }, _cache);

        var context = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(context, null!);

        // Assert
        Assert.True(nextInvoked);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task ActiveUser_FromCache_PassesThroughToNext()
    {
        // Arrange
        var userId = "user-active-123";
        _cache.Set($"account_status_{userId}", (AccountStatus?)AccountStatus.Active);

        bool nextInvoked = false;
        var middleware = new AccountStatusMiddleware(_ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        }, _cache);

        var context = new DefaultHttpContext();
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "TestAuth");
        context.User = new ClaimsPrincipal(identity);

        // Act
        await middleware.InvokeAsync(context, null!);

        // Assert
        Assert.True(nextInvoked);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Theory]
    [InlineData(AccountStatus.Suspended)]
    [InlineData(AccountStatus.Banned)]
    [InlineData(AccountStatus.Deleted)]
    [InlineData(AccountStatus.PendingDeletion)]
    public async Task InactiveUser_FromCache_ReturnsForbidden403(AccountStatus status)
    {
        // Arrange
        var userId = $"user-{status}-123";
        _cache.Set($"account_status_{userId}", (AccountStatus?)status);

        bool nextInvoked = false;
        var middleware = new AccountStatusMiddleware(_ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        }, _cache);

        var context = new DefaultHttpContext();
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "TestAuth");
        context.User = new ClaimsPrincipal(identity);

        // Act
        await middleware.InvokeAsync(context, null!);

        // Assert
        Assert.False(nextInvoked);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }
}
