using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Roam.Domain.Users;

namespace Roam.IntegrationTests.Infrastructure;

public sealed record TokenPair(string AccessToken, string RefreshToken);

/// <summary>Creates users directly (avoiding the /register rate limit) and signs them in over HTTP.</summary>
public static class TestUsers
{
    public const string Password = "Str0ng!Passw0rd";

    public static async Task<ApplicationUser> CreateAsync(IServiceProvider services, bool volunteer = false, bool admin = false)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"user-{Guid.NewGuid():N}@example.test";
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Test " + email[5..11],
            AgeCategory = AgeCategory.Adult,
            DateOfBirth = new DateOnly(1990, 1, 1),
            VolunteerStatus = volunteer ? VolunteerStatus.Approved : VolunteerStatus.None
        };

        var result = await userManager.CreateAsync(user, Password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        if (admin)
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roleManager.RoleExistsAsync("Admin"))
                await roleManager.CreateAsync(new IdentityRole("Admin"));
            await userManager.AddToRoleAsync(user, "Admin");
        }

        return user;
    }

    public static async Task<TokenPair> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new TokenPair(payload.GetProperty("accessToken").GetString()!, payload.GetProperty("refreshToken").GetString()!);
    }

    public static HttpClient AuthorizedClient<TEntry>(WebApplicationFactory<TEntry> factory, string accessToken) where TEntry : class
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }
}
