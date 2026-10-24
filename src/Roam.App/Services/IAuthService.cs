using System;
using System.Threading.Tasks;

namespace Roam.App.Services;

public interface IAuthService
{
    bool IsAuthenticated { get; }
    string? CurrentUserId { get; }
    string? CurrentUserEmail { get; }
    string? CurrentDisplayName { get; }
    int VolunteerStatus { get; }
    bool IsVolunteer { get; }

    event Action? AuthStateChanged;

    Task InitializeAsync();
    Task<string?> GetAccessTokenAsync();
    Task<bool> LoginAsync(string email, string password);
    Task<(bool Success, string? ErrorMessage)> RegisterAsync(string email, string password, string displayName, string country, DateTime dateOfBirth);
    Task<bool> RefreshTokenAsync();
    Task RefreshUserProfileAsync();
    Task LogoutAsync();
}
