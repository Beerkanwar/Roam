using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Roam.Domain.TourRequests;
using Roam.Domain.TourSessions;
using Roam.Infrastructure.Persistence;
using Roam.IntegrationTests.Infrastructure;
using Xunit;

namespace Roam.IntegrationTests.Api.Lifecycle;

public class AccountAndTourLifecycleTests : IClassFixture<IntegrationTestWebAppFactory>
{
    private readonly IntegrationTestWebAppFactory _factory;

    public AccountAndTourLifecycleTests(IntegrationTestWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task VolunteerWithdrawal_ReturnsRequestToDiscovery_RequesterCancelStillCancels()
    {
        var (requesterApi, volunteerApi, _, _) = await RequesterAndVolunteerAsync();
        var requestId = await CreateRequestAsync(requesterApi);
        Assert.Equal(HttpStatusCode.OK, (await volunteerApi.PostAsync($"/api/v1/tour-requests/{requestId}/accept", null)).StatusCode);

        var withdraw = await volunteerApi.PostAsJsonAsync($"/api/v1/tour-requests/{requestId}/cancel", new { reason = "Something came up" });
        Assert.Equal(HttpStatusCode.OK, withdraw.StatusCode);

        var afterWithdraw = await LoadRequestAsync(requestId);
        Assert.Equal(TourRequestStatus.Published, afterWithdraw.Status);
        Assert.Null(afterWithdraw.AssignedVolunteerId);

        var cancel = await requesterApi.PostAsJsonAsync($"/api/v1/tour-requests/{requestId}/cancel", new { reason = "No longer needed" });
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal(TourRequestStatus.Cancelled, (await LoadRequestAsync(requestId)).Status);
    }

    [Fact]
    public async Task DeletingVolunteerAccount_UnassignsPendingTours_AndEndsLiveSessions()
    {
        var (requesterApi, volunteerApi, _, volunteerId) = await RequesterAndVolunteerAsync();

        var pendingId = await CreateRequestAsync(requesterApi);
        Assert.Equal(HttpStatusCode.OK, (await volunteerApi.PostAsync($"/api/v1/tour-requests/{pendingId}/accept", null)).StatusCode);

        var liveId = await CreateRequestAsync(requesterApi);
        Assert.Equal(HttpStatusCode.OK, (await volunteerApi.PostAsync($"/api/v1/tour-requests/{liveId}/accept", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await volunteerApi.PostAsync($"/api/v1/tour-requests/{liveId}/start", null)).StatusCode);

        var delete = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/profile/me")
        {
            Content = JsonContent.Create(new { password = TestUsers.Password })
        };
        Assert.Equal(HttpStatusCode.NoContent, (await volunteerApi.SendAsync(delete)).StatusCode);

        var pending = await LoadRequestAsync(pendingId);
        Assert.Equal(TourRequestStatus.Published, pending.Status);
        Assert.Null(pending.AssignedVolunteerId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var session = db.TourSessions.Single(s => s.TourRequestId == liveId);
        Assert.Equal(SessionStatus.Ended, session.Status);
        Assert.NotNull(session.EndedAt);
        Assert.Equal(TourRequestStatus.Completed, (await db.TourRequests.FindAsync(liveId))!.Status);
        Assert.False(db.TourRequests.Any(r => r.AssignedVolunteerId == volunteerId &&
            (r.Status == TourRequestStatus.Accepted || r.Status == TourRequestStatus.VolunteerInterested || r.Status == TourRequestStatus.Confirmed)));
    }

    [Fact]
    public async Task VolunteerApproval_KeepsRefreshWorking_AndPermissionChangesApplyToExistingTokens()
    {
        var user = await TestUsers.CreateAsync(_factory.Services);
        var admin = await TestUsers.CreateAsync(_factory.Services, admin: true);
        var userTokens = await TestUsers.LoginAsync(_factory.CreateClient(), user.Email!);
        var adminApi = TestUsers.AuthorizedClient(_factory, (await TestUsers.LoginAsync(_factory.CreateClient(), admin.Email!)).AccessToken);
        var userApi = TestUsers.AuthorizedClient(_factory, userTokens.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await userApi.GetAsync("/api/v1/tour-requests")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await adminApi.PostAsync($"/api/v1/admin/volunteers/{user.Id}/approve", null)).StatusCode);

        // The token was issued before approval; the live claims make the volunteer endpoint available now.
        Assert.Equal(HttpStatusCode.OK, (await userApi.GetAsync("/api/v1/tour-requests")).StatusCode);

        // Approval used to rotate the security stamp, which made /refresh reject the user's refresh token.
        var refresh = await _factory.CreateClient().PostAsJsonAsync("/refresh", new { refreshToken = userTokens.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await adminApi.PostAsync($"/api/v1/admin/volunteers/{user.Id}/reject", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await userApi.GetAsync("/api/v1/tour-requests")).StatusCode);
    }

    [Fact]
    public async Task MyTours_ListsEachSideWithItsRole_AndEndingSessionFromInsideCompletesTour()
    {
        var (requesterApi, volunteerApi, requesterId, volunteerId) = await RequesterAndVolunteerAsync();
        var requestId = await CreateRequestAsync(requesterApi);
        Assert.Equal(HttpStatusCode.OK, (await volunteerApi.PostAsync($"/api/v1/tour-requests/{requestId}/accept", null)).StatusCode);

        var requesterTours = await requesterApi.GetFromJsonAsync<JsonElement>("/api/v1/tour-requests/mine");
        Assert.Contains(requesterTours.EnumerateArray(), t => t.GetProperty("id").GetString() == requestId && t.GetProperty("role").GetString() == "Requester");
        var volunteerTours = await volunteerApi.GetFromJsonAsync<JsonElement>("/api/v1/tour-requests/mine");
        Assert.Contains(volunteerTours.EnumerateArray(), t => t.GetProperty("id").GetString() == requestId && t.GetProperty("role").GetString() == "Volunteer");

        // Each side learns who the other person is (used for the live-tour title, feedback and reports).
        var asVolunteer = volunteerTours.EnumerateArray().Single(t => t.GetProperty("id").GetString() == requestId);
        Assert.Equal(requesterId, asVolunteer.GetProperty("counterpartId").GetString());
        Assert.StartsWith("Test ", asVolunteer.GetProperty("counterpartName").GetString());
        var asRequester = requesterTours.EnumerateArray().Single(t => t.GetProperty("id").GetString() == requestId);
        Assert.Equal(volunteerId, asRequester.GetProperty("counterpartId").GetString());

        var start = await volunteerApi.PostAsync($"/api/v1/tour-requests/{requestId}/start", null);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var sessionId = (await start.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sessionId").GetString()!;

        Assert.Equal(HttpStatusCode.OK, (await requesterApi.PostAsync($"/api/v1/sessions/{sessionId}/end", null)).StatusCode);
        // Both participants may press "End tour"; the second call is not an error.
        Assert.Equal(HttpStatusCode.OK, (await volunteerApi.PostAsync($"/api/v1/sessions/{sessionId}/end", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await volunteerApi.PostAsync("/api/v1/sessions/does-not-exist/end", null)).StatusCode);

        Assert.Equal(TourRequestStatus.Completed, (await LoadRequestAsync(requestId)).Status);
        var afterEnd = await requesterApi.GetFromJsonAsync<JsonElement>("/api/v1/tour-requests/mine");
        Assert.DoesNotContain(afterEnd.EnumerateArray(), t => t.GetProperty("id").GetString() == requestId);
    }

    private async Task<(HttpClient RequesterApi, HttpClient VolunteerApi, string RequesterId, string VolunteerId)> RequesterAndVolunteerAsync()
    {
        var requester = await TestUsers.CreateAsync(_factory.Services);
        var volunteer = await TestUsers.CreateAsync(_factory.Services, volunteer: true);
        var requesterApi = TestUsers.AuthorizedClient(_factory, (await TestUsers.LoginAsync(_factory.CreateClient(), requester.Email!)).AccessToken);
        var volunteerApi = TestUsers.AuthorizedClient(_factory, (await TestUsers.LoginAsync(_factory.CreateClient(), volunteer.Email!)).AccessToken);
        return (requesterApi, volunteerApi, requester.Id, volunteer.Id);
    }

    private static async Task<string> CreateRequestAsync(HttpClient requesterApi)
    {
        var response = await requesterApi.PostAsJsonAsync("/api/v1/tour-requests/coordinates", new
        {
            latitude = 40.6892 + Random.Shared.NextDouble() / 100, longitude = -74.0445, visibility = 0, mode = 0, description = "Lifecycle test"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;
    }

    private async Task<TourRequest> LoadRequestAsync(string id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return (await db.TourRequests.FindAsync(id))!;
    }
}
