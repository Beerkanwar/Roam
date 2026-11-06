using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using Roam.Contracts.Users;

namespace Roam.App.Services;

public class AuthService : IAuthService
{
    private const string AccessTokenKey = "roam_auth_access_token";
    private const string RefreshTokenKey = "roam_auth_refresh_token";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ApiClient _apiClient;
    private string? _cachedAccessToken;
    private string? _cachedRefreshToken;

    public bool IsAuthenticated => !string.IsNullOrEmpty(_cachedAccessToken);
    public string? CurrentUserId { get; private set; }
    public string? CurrentUserEmail { get; private set; }
    public string? CurrentDisplayName { get; private set; }
    public VolunteerStatus VolunteerStatus { get; private set; } = VolunteerStatus.None;
    public bool IsVolunteer => VolunteerStatus == VolunteerStatus.Approved;

    public event Action? AuthStateChanged;

    public AuthService(ApiClient apiClient)
    {
        _apiClient = apiClient;
        _apiClient.TokenProvider = GetAccessTokenAsync;
        _apiClient.RefreshTokenHandler = RefreshTokenAsync;
    }

    public async Task InitializeAsync()
    {
        _cachedAccessToken = await GetSecureValueAsync(AccessTokenKey);
        _cachedRefreshToken = await GetSecureValueAsync(RefreshTokenKey);

        if (!string.IsNullOrEmpty(_cachedAccessToken))
        {
            await FetchUserProfileAsync();
        }

        AuthStateChanged?.Invoke();
    }

    public Task<string?> GetAccessTokenAsync()
    {
        return Task.FromResult(_cachedAccessToken);
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return false;

        try
        {
            var payload = new { email = email.Trim(), password };
            var response = await _apiClient.PostAsync<object, LoginResponse>("/login", payload, requireAuth: false);

            if (response != null && !string.IsNullOrEmpty(response.AccessToken))
            {
                _cachedAccessToken = response.AccessToken;
                _cachedRefreshToken = response.RefreshToken;

                await SetSecureValueAsync(AccessTokenKey, _cachedAccessToken);
                if (!string.IsNullOrEmpty(_cachedRefreshToken))
                {
                    await SetSecureValueAsync(RefreshTokenKey, _cachedRefreshToken);
                }

                CurrentUserEmail = email.Trim();
                await FetchUserProfileAsync();
                AuthStateChanged?.Invoke();
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> RegisterAsync(
        string email, string password, string displayName, string country, DateTime dateOfBirth)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return (false, "Email and password are required.");

        try
        {
            // 1. Register with Identity
            var regPayload = new { email = email.Trim(), password };
            var regSuccess = await _apiClient.PostAsync("/register", regPayload, requireAuth: false);
            if (!regSuccess)
            {
                return (false, "Registration failed. Ensure password contains digits, lowercase, and symbols.");
            }

            // 2. Automatically log in to get token
            var loginSuccess = await LoginAsync(email, password);
            if (!loginSuccess)
            {
                return (false, "Account created, but automatic sign-in failed. Please log in manually.");
            }

            // 3. Complete onboarding with 18+ Safeguarding
            var dateOnly = DateOnly.FromDateTime(dateOfBirth);
            var onboardingPayload = new
            {
                displayName = displayName.Trim(),
                country = country.Trim(),
                dateOfBirth = dateOnly
            };

            var onboardingSuccess = await _apiClient.PostAsync("/api/v1/profile/onboarding", onboardingPayload, requireAuth: true);
            if (!onboardingSuccess)
            {
                return (false, "Safeguarding age verification failed. You must be at least 18 years old to complete registration.");
            }

            await FetchUserProfileAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<bool> RefreshTokenAsync()
    {
        if (string.IsNullOrEmpty(_cachedRefreshToken))
        {
            await LogoutAsync();
            return false;
        }

        try
        {
            var payload = new { refreshToken = _cachedRefreshToken };
            var response = await _apiClient.PostAsync<object, LoginResponse>("/refresh", payload, requireAuth: false);

            if (response != null && !string.IsNullOrEmpty(response.AccessToken))
            {
                _cachedAccessToken = response.AccessToken;
                await SetSecureValueAsync(AccessTokenKey, _cachedAccessToken);

                if (!string.IsNullOrEmpty(response.RefreshToken))
                {
                    _cachedRefreshToken = response.RefreshToken;
                    await SetSecureValueAsync(RefreshTokenKey, _cachedRefreshToken);
                }

                AuthStateChanged?.Invoke();
                return true;
            }

            await LogoutAsync();
            return false;
        }
        catch
        {
            await LogoutAsync();
            return false;
        }
    }

    public async Task LogoutAsync()
    {
        _cachedAccessToken = null;
        _cachedRefreshToken = null;
        CurrentUserId = null;
        CurrentUserEmail = null;
        CurrentDisplayName = null;
        VolunteerStatus = VolunteerStatus.None;

        RemoveSecureValue(AccessTokenKey);
        RemoveSecureValue(RefreshTokenKey);

        AuthStateChanged?.Invoke();
        await Task.CompletedTask;
    }

    public async Task RefreshUserProfileAsync()
    {
        await FetchUserProfileAsync();
        AuthStateChanged?.Invoke();
    }

    private async Task FetchUserProfileAsync()
    {
        try
        {
            var profile = await _apiClient.GetAsync<UserProfileDto>("/api/v1/profile/me", requireAuth: true);
            if (profile != null)
            {
                CurrentUserId = profile.Id;
                CurrentUserEmail = profile.Email ?? CurrentUserEmail;
                CurrentDisplayName = !string.IsNullOrWhiteSpace(profile.DisplayName) ? profile.DisplayName : CurrentUserEmail;
                VolunteerStatus = profile.VolunteerStatus;
            }
        }
        catch
        {
            // Non-fatal if offline; retains token
        }
    }

    private static async Task<string?> GetSecureValueAsync(string key)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(key);
        }
        catch
        {
            return null;
        }
    }

    private static async Task SetSecureValueAsync(string key, string value)
    {
        await SecureStorage.Default.SetAsync(key, value);
    }

    private static void RemoveSecureValue(string key)
    {
        SecureStorage.Default.Remove(key);
    }

    private class LoginResponse
    {
        public string? TokenType { get; set; }
        public string? AccessToken { get; set; }
        public int? ExpiresIn { get; set; }
        public string? RefreshToken { get; set; }
    }

    private class UserProfileDto
    {
        public string? Id { get; set; }
        public string? Email { get; set; }
        public string? DisplayName { get; set; }
        public string? Country { get; set; }
        public VolunteerStatus VolunteerStatus { get; set; }
    }
}
