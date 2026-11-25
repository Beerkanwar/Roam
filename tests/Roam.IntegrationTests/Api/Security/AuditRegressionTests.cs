using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Roam.Domain.Payments;
using Roam.Domain.TrustAndSafety;
using Roam.Domain.Users;
using Roam.Infrastructure.Persistence;
using Roam.IntegrationTests.Infrastructure;
using Xunit;

namespace Roam.IntegrationTests.Api.Security;

/// <summary>
/// End-to-end regression tests (real HTTP pipeline + PostgreSQL) for security fixes from the repository audit.
/// Users are created directly through UserManager so only /login hits the 10-per-minute auth rate limit.
/// </summary>
public class AuditRegressionTests : IClassFixture<IntegrationTestWebAppFactory>
{
    private const string Password = "Str0ng!Passw0rd";
    private readonly IntegrationTestWebAppFactory _factory;

    public AuditRegressionTests(IntegrationTestWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TipSettlement_IsRejectedForParticipants_AndAllowedForAdmins()
    {
        var sender = await CreateUserAsync(volunteer: false);
        var receiver = await CreateUserAsync(volunteer: true);
        var admin = await CreateUserAsync(volunteer: false, admin: true);

        string tipId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tip = new TipTransaction(sender.Id, receiver.Id, 10m, "USD");
            db.TipTransactions.Add(tip);
            await db.SaveChangesAsync();
            tipId = tip.Id;
        }

        var senderClient = await LoginAsync(sender.Email!);
        var receiverClient = await LoginAsync(receiver.Email!);
        var adminClient = await LoginAsync(admin.Email!);

        var senderAttempt = await senderClient.PostAsync($"/api/v1/payments/tips/{tipId}/settle?succeeded=true", null);
        var receiverAttempt = await receiverClient.PostAsync($"/api/v1/payments/tips/{tipId}/settle?succeeded=true", null);
        Assert.Equal(HttpStatusCode.Forbidden, senderAttempt.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, receiverAttempt.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stored = await db.TipTransactions.FindAsync(tipId);
            Assert.Equal(PaymentStatus.Pending, stored!.Status);
        }

        var adminAttempt = await adminClient.PostAsync($"/api/v1/payments/tips/{tipId}/settle?succeeded=true", null);
        Assert.Equal(HttpStatusCode.OK, adminAttempt.StatusCode);
    }

    [Fact]
    public async Task DataExport_Succeeds_ForVolunteerWithSettings_AndHidesSecretsAndReporterIdentity()
    {
        var volunteer = await CreateUserAsync(volunteer: true);
        var reporter = await CreateUserAsync(volunteer: false);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.VolunteerSettings.Add(new VolunteerSettings { UserId = volunteer.Id, Enabled = true, Languages = "en,fr" });
            db.Reports.Add(new Report
            {
                ReporterId = reporter.Id,
                ReportedUserId = volunteer.Id,
                Reason = ReportReason.Harassment,
                Description = "confidential reporter statement"
            });
            await db.SaveChangesAsync();
        }

        var client = await LoginAsync(volunteer.Email!);
        var response = await client.GetAsync("/api/v1/profile/me/export");
        var body = await response.Content.ReadAsStringAsync();

        // Previously: VolunteerSettings.User -> ApplicationUser -> VolunteerSettings object cycle => HTTP 500.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(body);
        var profile = json.RootElement.GetProperty("profile");
        Assert.Equal("en,fr", profile.GetProperty("volunteerSettings").GetProperty("languages").GetString());

        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("securityStamp", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(reporter.Id, body);
        Assert.DoesNotContain("confidential reporter statement", body);

        var received = json.RootElement.GetProperty("reports").GetProperty("received");
        Assert.Equal(1, received.GetArrayLength());
    }

    [Fact]
    public async Task Onboarding_RejectsScriptProfilePhotoUrl()
    {
        var user = await CreateUserAsync(volunteer: false);
        var client = await LoginAsync(user.Email!);

        var response = await client.PostAsJsonAsync("/api/v1/profile/onboarding", new
        {
            displayName = "Tester",
            country = "NZ",
            dateOfBirth = "1990-01-01",
            profilePhotoUrl = "javascript:alert(1)"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BlockingUnknownUser_Returns400_NotServerError()
    {
        var user = await CreateUserAsync(volunteer: false);
        var client = await LoginAsync(user.Email!);

        var response = await client.PostAsync($"/api/v1/trust-safety/blocks/{Guid.NewGuid()}", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TourLifecycle_InterestAcceptAndRepeatedStart_CreatesExactlyOneSession()
    {
        var requester = await CreateUserAsync(volunteer: false);
        var volunteer = await CreateUserAsync(volunteer: true);
        var requesterClient = await LoginAsync(requester.Email!);
        var volunteerClient = await LoginAsync(volunteer.Email!);

        var create = await requesterClient.PostAsJsonAsync("/api/v1/tour-requests/coordinates", new
        {
            latitude = 51.5007,
            longitude = -0.1246,
            visibility = 0,
            mode = 0,
            description = "Walk past the clock tower"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var requestId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        // Undefined enum values are rejected instead of being persisted.
        var badMode = await requesterClient.PostAsJsonAsync("/api/v1/tour-requests/coordinates", new
        {
            latitude = 51.5007, longitude = -0.1246, visibility = 0, mode = 99, description = "x"
        });
        Assert.Equal(HttpStatusCode.BadRequest, badMode.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await volunteerClient.PostAsync($"/api/v1/tour-requests/{requestId}/interest", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await volunteerClient.PostAsync($"/api/v1/tour-requests/{requestId}/accept", null)).StatusCode);

        var firstStart = await volunteerClient.PostAsync($"/api/v1/tour-requests/{requestId}/start", null);
        var secondStart = await volunteerClient.PostAsync($"/api/v1/tour-requests/{requestId}/start", null);
        Assert.Equal(HttpStatusCode.OK, firstStart.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondStart.StatusCode);

        var firstSessionId = (await firstStart.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sessionId").GetString();
        var secondSessionId = (await secondStart.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sessionId").GetString();
        Assert.False(string.IsNullOrEmpty(firstSessionId));
        Assert.Equal(firstSessionId, secondSessionId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, db.TourSessions.Count(s => s.TourRequestId == requestId));
        Assert.Equal(Roam.Domain.TourRequests.TourRequestStatus.Active, (await db.TourRequests.FindAsync(requestId))!.Status);
    }

    private async Task<ApplicationUser> CreateUserAsync(bool volunteer, bool admin = false)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"user-{Guid.NewGuid():N}@example.test";
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Test User",
            AgeCategory = AgeCategory.Adult,
            DateOfBirth = new DateOnly(1990, 1, 1),
            VolunteerStatus = volunteer ? VolunteerStatus.Approved : VolunteerStatus.None
        };

        var result = await userManager.CreateAsync(user, Password);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));

        if (admin)
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roleManager.RoleExistsAsync("Admin"))
                await roleManager.CreateAsync(new IdentityRole("Admin"));
            await userManager.AddToRoleAsync(user, "Admin");
        }

        return user;
    }

    private async Task<HttpClient> LoginAsync(string email)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = payload.GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
