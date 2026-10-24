using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Roam.Api.Hubs;
using Roam.Domain.Users;
using Xunit;

namespace Roam.Api.Tests.Hubs;

public class SignalRSecurityFilterTests
{
    private class TestHub : Hub
    {
        public void Ping() { }
    }

    private class TestHubCallerContext : HubCallerContext
    {
        private readonly ClaimsPrincipal? _user;

        public TestHubCallerContext(string connectionId, ClaimsPrincipal? user = null)
        {
            ConnectionId = connectionId;
            _user = user;
        }

        public override string ConnectionId { get; }
        public override string? UserIdentifier => _user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        public override ClaimsPrincipal? User => _user;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features => new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;

        public bool Aborted { get; private set; }
        public override void Abort() => Aborted = true;
    }

    [Fact]
    public async Task InvokeMethodAsync_EnforcesRateLimit_WhenExceedingThreshold()
    {
        // Arrange
        var services = new ServiceCollection().BuildServiceProvider();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var filter = new SignalRSecurityFilter(services, cache);

        var context = new TestHubCallerContext("conn-rate-test-1");
        var hub = new TestHub();
        var method = typeof(TestHub).GetMethod(nameof(TestHub.Ping))!;
        var invocationContext = new HubInvocationContext(context, services, hub, method, Array.Empty<object?>());

        // Act & Assert: first 180 calls should succeed
        for (int i = 0; i < 180; i++)
        {
            var result = await filter.InvokeMethodAsync(invocationContext, _ => ValueTask.FromResult<object?>("ok"));
            Assert.Equal("ok", result);
        }

        // 181st call should trip rate limiter
        var ex = await Assert.ThrowsAsync<HubException>(() =>
            filter.InvokeMethodAsync(invocationContext, _ => ValueTask.FromResult<object?>("ok")).AsTask());

        Assert.Contains("Too many requests", ex.Message);
    }

    [Fact]
    public async Task InvokeMethodAsync_AllowsCallsUnderLimit()
    {
        // Arrange
        var services = new ServiceCollection().BuildServiceProvider();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var filter = new SignalRSecurityFilter(services, cache);

        var context = new TestHubCallerContext("conn-allow-test");
        var hub = new TestHub();
        var method = typeof(TestHub).GetMethod(nameof(TestHub.Ping))!;
        var invocationContext = new HubInvocationContext(context, services, hub, method, Array.Empty<object?>());

        // Act
        var result = await filter.InvokeMethodAsync(invocationContext, _ => ValueTask.FromResult<object?>("passed"));

        // Assert
        Assert.Equal("passed", result);
        Assert.False(context.Aborted);
    }
}
