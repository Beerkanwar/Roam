using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Roam.Contracts.Chat;
using Roam.Infrastructure.Persistence;
using Roam.Infrastructure.Scaling;
using Roam.IntegrationTests.Infrastructure;
using StackExchange.Redis;
using Xunit;

namespace Roam.IntegrationTests.Api.Scaling;

/// <summary>
/// Runs two API instances against shared PostgreSQL + Redis and checks that a user can be served by
/// either instance at any time: tokens, rate limits, SignalR signalling, chat and notifications.
/// </summary>
public class MultiServerTests : IClassFixture<MultiServerFixture>
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(10);
    private readonly MultiServerFixture _servers;

    public MultiServerTests(MultiServerFixture servers)
    {
        _servers = servers;
    }

    [Fact]
    public void BothServers_UseRedis()
    {
        Assert.NotNull(_servers.ServerA.Services.GetService<IConnectionMultiplexer>());
        Assert.NotNull(_servers.ServerB.Services.GetService<IConnectionMultiplexer>());
    }

    [Fact]
    public async Task TokensIssuedByOneServer_AreAcceptedAndRefreshedByTheOther()
    {
        var user = await TestUsers.CreateAsync(_servers.ServerA.Services);
        var tokens = await TestUsers.LoginAsync(_servers.ServerA.CreateClient(), user.Email!);

        var onB = TestUsers.AuthorizedClient(_servers.ServerB, tokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await onB.GetAsync("/api/v1/profile/me")).StatusCode);

        var refresh = await _servers.ServerB.CreateClient().PostAsJsonAsync("/refresh", new { refreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var refreshed = (await refresh.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;

        var backOnA = TestUsers.AuthorizedClient(_servers.ServerA, refreshed);
        Assert.Equal(HttpStatusCode.OK, (await backOnA.GetAsync("/api/v1/profile/me")).StatusCode);
    }

    [Fact]
    public async Task PerUserRateLimit_IsEnforcedAcrossServers()
    {
        // UserPolicy allows 60 requests/minute per user. Alternating servers, a per-instance limiter
        // would allow 35 + 35 = 70; the shared limiter must reject everything after the 60th.
        var user = await TestUsers.CreateAsync(_servers.ServerA.Services);
        var tokens = await TestUsers.LoginAsync(_servers.ServerA.CreateClient(), user.Email!);
        var clients = new[]
        {
            TestUsers.AuthorizedClient(_servers.ServerA, tokens.AccessToken),
            TestUsers.AuthorizedClient(_servers.ServerB, tokens.AccessToken)
        };

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 70; i++)
            statuses.Add((await clients[i % 2].GetAsync("/api/v1/profile/me")).StatusCode);

        Assert.Equal(60, statuses.Count(s => s == HttpStatusCode.OK));
        Assert.Equal(10, statuses.Count(s => s == HttpStatusCode.TooManyRequests));
        Assert.All(statuses.Skip(60), s => Assert.Equal(HttpStatusCode.TooManyRequests, s));
    }

    [Fact]
    public async Task LiveTour_SignallingChatAndNotifications_WorkAcrossServers()
    {
        var requester = await TestUsers.CreateAsync(_servers.ServerA.Services);
        var volunteer = await TestUsers.CreateAsync(_servers.ServerA.Services, volunteer: true);
        var requesterTokens = await TestUsers.LoginAsync(_servers.ServerA.CreateClient(), requester.Email!);
        var volunteerTokens = await TestUsers.LoginAsync(_servers.ServerB.CreateClient(), volunteer.Email!);
        var requesterApi = TestUsers.AuthorizedClient(_servers.ServerA, requesterTokens.AccessToken);
        var volunteerApi = TestUsers.AuthorizedClient(_servers.ServerB, volunteerTokens.AccessToken);

        // Requester listens for notifications on server B; the volunteer will start the tour on server A.
        var tourStarting = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var requesterNotifications = Connect(_servers.ServerB.Server, "/hubs/notifications", requesterTokens.AccessToken);
        requesterNotifications.On<string>("TourStarting", id => tourStarting.TrySetResult(id));
        await requesterNotifications.StartAsync();

        var create = await requesterApi.PostAsJsonAsync("/api/v1/tour-requests/coordinates", new
        {
            latitude = 48.8584, longitude = 2.2945, visibility = 0, mode = 0, description = "Cross-server tour"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var requestId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.OK, (await volunteerApi.PostAsync($"/api/v1/tour-requests/{requestId}/accept", null)).StatusCode);

        var volunteerOnA = TestUsers.AuthorizedClient(_servers.ServerA, volunteerTokens.AccessToken);
        var start = await volunteerOnA.PostAsync($"/api/v1/tour-requests/{requestId}/start", null);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var sessionId = (await start.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sessionId").GetString()!;

        Assert.Equal(sessionId, await tourStarting.Task.WaitAsync(EventTimeout));

        // Requester's tour/chat hubs on server A, volunteer's on server B.
        await using var requesterTour = Connect(_servers.ServerA.Server, "/hubs/tour", requesterTokens.AccessToken);
        await using var volunteerTour = Connect(_servers.ServerB.Server, "/hubs/tour", volunteerTokens.AccessToken);
        await using var requesterChat = Connect(_servers.ServerA.Server, "/hubs/chat", requesterTokens.AccessToken);
        await using var volunteerChat = Connect(_servers.ServerB.Server, "/hubs/chat", volunteerTokens.AccessToken);

        // With negotiation skipped (as the real clients connect) HubConnection.ConnectionId is not known
        // client-side, so connection ids are learned from the server's ParticipantJoined broadcasts.
        var joinedOnRequesterSide = System.Threading.Channels.Channel.CreateUnbounded<string>();
        var offerReceived = new TaskCompletionSource<(string From, string Sdp)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var chatReceived = new TaskCompletionSource<ChatMessageDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var volunteerLeft = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        requesterTour.On<string>("ParticipantJoined", id => joinedOnRequesterSide.Writer.TryWrite(id));
        requesterTour.On<string>("ParticipantLeft", id => volunteerLeft.TrySetResult(id));
        volunteerTour.On<string, string>("ReceiveOffer", (from, sdp) => offerReceived.TrySetResult((from, sdp)));
        volunteerChat.On<ChatMessageDto>("ChatMessageReceived", m => { if (m.SenderId == requester.Id) chatReceived.TrySetResult(m); });

        await Task.WhenAll(requesterTour.StartAsync(), volunteerTour.StartAsync(), requesterChat.StartAsync(), volunteerChat.StartAsync());
        await requesterTour.InvokeAsync("JoinSession", sessionId);
        var requesterConnectionId = await joinedOnRequesterSide.Reader.ReadAsync().AsTask().WaitAsync(EventTimeout);
        await volunteerTour.InvokeAsync("JoinSession", sessionId);
        await requesterChat.InvokeAsync("JoinSessionChat", sessionId);
        await volunteerChat.InvokeAsync("JoinSessionChat", sessionId);

        // Server A must validate a target connection that lives on server B, then deliver to it.
        var volunteerConnectionId = await joinedOnRequesterSide.Reader.ReadAsync().AsTask().WaitAsync(EventTimeout);
        Assert.NotEqual(requesterConnectionId, volunteerConnectionId);
        await requesterTour.InvokeAsync("SendOffer", sessionId, "{\"type\":\"offer\",\"sdp\":\"v=0\"}", volunteerConnectionId);
        var offer = await offerReceived.Task.WaitAsync(EventTimeout);
        Assert.Equal(requesterConnectionId, offer.From);

        await requesterChat.InvokeAsync("SendMessage", sessionId, "Hello from server A");
        Assert.Equal("Hello from server A", (await chatReceived.Task.WaitAsync(EventTimeout)).Text);

        // A dropped connection on server B is announced to the peer on server A.
        await volunteerTour.StopAsync();
        Assert.Equal(volunteerConnectionId, await volunteerLeft.Task.WaitAsync(EventTimeout));
    }

    [Fact]
    public async Task AdvisoryLock_AllowsOnlyOneInstanceAtATime()
    {
        using var scopeA = _servers.ServerA.Services.CreateScope();
        using var scopeB = _servers.ServerB.Services.CreateScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        const long lockId = 0x7E57_0001;

        var releaseA = new TaskCompletionSource();
        var aHoldsLock = new TaskCompletionSource();
        var aRun = PostgresAdvisoryLock.TryRunExclusiveAsync(dbA, lockId, async _ =>
        {
            aHoldsLock.SetResult();
            await releaseA.Task;
        }, CancellationToken.None);

        await aHoldsLock.Task.WaitAsync(EventTimeout);
        var bRanWhileHeld = await PostgresAdvisoryLock.TryRunExclusiveAsync(dbB, lockId, _ => Task.CompletedTask, CancellationToken.None);
        releaseA.SetResult();
        Assert.True(await aRun);
        Assert.False(bRanWhileHeld);

        var bRanAfterRelease = await PostgresAdvisoryLock.TryRunExclusiveAsync(dbB, lockId, _ => Task.CompletedTask, CancellationToken.None);
        Assert.True(bRanAfterRelease);
    }

    /// <summary>Connects like the real clients do: WebSockets only, negotiation skipped (no sticky sessions).</summary>
    private static HubConnection Connect(TestServer server, string path, string accessToken)
    {
        return new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, path), options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.SkipNegotiation = true;
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                options.WebSocketFactory = async (context, cancellationToken) =>
                {
                    var client = server.CreateWebSocketClient();
                    client.ConfigureRequest = request => request.Headers.Authorization = $"Bearer {accessToken}";
                    return await client.ConnectAsync(context.Uri, cancellationToken);
                };
            })
            .Build();
    }

    [Fact]
    public async Task PermissionChanges_ApplyOnOtherServers_WithoutWaitingForCacheExpiry()
    {
        var user = await TestUsers.CreateAsync(_servers.ServerA.Services);
        var admin = await TestUsers.CreateAsync(_servers.ServerA.Services, admin: true);
        var adminOnA = TestUsers.AuthorizedClient(_servers.ServerA, (await TestUsers.LoginAsync(_servers.ServerA.CreateClient(), admin.Email!)).AccessToken);
        var userOnB = TestUsers.AuthorizedClient(_servers.ServerB, (await TestUsers.LoginAsync(_servers.ServerA.CreateClient(), user.Email!)).AccessToken);

        // Server B caches "not a volunteer" for this user.
        Assert.Equal(HttpStatusCode.Forbidden, (await userOnB.GetAsync("/api/v1/tour-requests")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await adminOnA.PostAsync($"/api/v1/admin/volunteers/{user.Id}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, await StatusWithinAsync(userOnB, "/api/v1/tour-requests", HttpStatusCode.OK));

        Assert.Equal(HttpStatusCode.OK, (await adminOnA.PostAsync($"/api/v1/admin/volunteers/{user.Id}/reject", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, await StatusWithinAsync(userOnB, "/api/v1/tour-requests", HttpStatusCode.Forbidden));
    }

    [Fact]
    public async Task AdminReports_ListOpenReports_AndSuspensionBlocksTheUserOnEveryServer()
    {
        var reporter = await TestUsers.CreateAsync(_servers.ServerA.Services);
        var offender = await TestUsers.CreateAsync(_servers.ServerA.Services);
        var admin = await TestUsers.CreateAsync(_servers.ServerA.Services, admin: true);
        var reporterOnA = TestUsers.AuthorizedClient(_servers.ServerA, (await TestUsers.LoginAsync(_servers.ServerA.CreateClient(), reporter.Email!)).AccessToken);
        var offenderOnB = TestUsers.AuthorizedClient(_servers.ServerB, (await TestUsers.LoginAsync(_servers.ServerB.CreateClient(), offender.Email!)).AccessToken);
        var adminOnA = TestUsers.AuthorizedClient(_servers.ServerA, (await TestUsers.LoginAsync(_servers.ServerA.CreateClient(), admin.Email!)).AccessToken);

        // Non-admins cannot see the moderation queue; admins see isAdmin on their profile.
        Assert.Equal(HttpStatusCode.Forbidden, (await reporterOnA.GetAsync("/api/v1/admin/reports")).StatusCode);
        var adminProfile = await adminOnA.GetFromJsonAsync<JsonElement>("/api/v1/profile/me");
        Assert.True(adminProfile.GetProperty("isAdmin").GetBoolean());
        Assert.False((await reporterOnA.GetFromJsonAsync<JsonElement>("/api/v1/profile/me")).GetProperty("isAdmin").GetBoolean());

        var submit = await reporterOnA.PostAsJsonAsync("/api/v1/trust-safety/reports",
            new { reportedUserId = offender.Id, reason = 0, severity = 2, description = "Abusive in chat" });
        Assert.Equal(HttpStatusCode.Created, submit.StatusCode);

        // The offender is active and cached as such on server B.
        Assert.Equal(HttpStatusCode.OK, (await offenderOnB.GetAsync("/api/v1/profile/me")).StatusCode);

        var reports = await adminOnA.GetFromJsonAsync<JsonElement>("/api/v1/admin/reports");
        var report = reports.EnumerateArray().Single(r => r.GetProperty("reportedUserId").GetString() == offender.Id);
        Assert.Equal("Harassment", report.GetProperty("reason").GetString());
        Assert.Equal("High", report.GetProperty("severity").GetString());
        Assert.Equal(reporter.Email, report.GetProperty("reporterEmail").GetString());
        var reportId = report.GetProperty("id").GetString();

        var resolve = await adminOnA.PostAsJsonAsync($"/api/v1/trust-safety/moderation/resolve/{reportId}", new { action = 3, reason = "Confirmed abuse" });
        Assert.Equal(HttpStatusCode.OK, resolve.StatusCode);

        // Suspended on server A; server B must stop serving the existing token right away.
        var status = await StatusWithinAsync(offenderOnB, "/api/v1/profile/me", HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
        Assert.NotEqual(HttpStatusCode.OK, status);

        var after = await adminOnA.GetFromJsonAsync<JsonElement>("/api/v1/admin/reports");
        Assert.DoesNotContain(after.EnumerateArray(), r => r.GetProperty("id").GetString() == reportId);
    }

    /// <summary>
    /// Polls for up to 3 seconds (well under the 15-second cache lifetime) until one of the expected
    /// statuses is returned; returns the last status seen.
    /// </summary>
    private static async Task<HttpStatusCode> StatusWithinAsync(HttpClient client, string url, params HttpStatusCode[] expected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        HttpStatusCode status;
        do
        {
            status = (await client.GetAsync(url)).StatusCode;
            if (expected.Contains(status)) return status;
            await Task.Delay(100);
        } while (DateTime.UtcNow < deadline);
        return status;
    }
}
