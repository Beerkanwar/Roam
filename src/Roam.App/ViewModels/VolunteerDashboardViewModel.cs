using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Roam.App.Models;
using Roam.App.Services;

namespace Roam.App.ViewModels;

public class VolunteerDashboardViewModel : BaseViewModel
{
    private readonly IApiClient _apiClient;
    private readonly IAuthService _authService;
    private readonly LocationService _locationService;

    private bool _isAvailable;
    private double _radiusKm = 10.0;
    private string _currentLocationText = "Location: Not synced";
    private bool _hasRequests;
    private bool _isRefreshing;
    private double _lastLatitude;
    private double _lastLongitude;
    private bool _hasLocation;

    public bool HasLocation
    {
        get => _hasLocation;
        set => SetProperty(ref _hasLocation, value);
    }

    public bool IsVolunteer => _authService.IsVolunteer;
    public bool IsNotVolunteer => !_authService.IsVolunteer;

    public string VolunteerStatusText => _authService.VolunteerStatus switch
    {
        1 => "Application Pending Approval",
        2 => "Verified Active Volunteer",
        3 => "Application Declined",
        4 => "Privileges Revoked",
        _ => "Not Registered as Volunteer"
    };

    public bool IsAvailable
    {
        get => _isAvailable;
        set => SetProperty(ref _isAvailable, value);
    }

    public double RadiusKm
    {
        get => _radiusKm;
        set
        {
            if (SetProperty(ref _radiusKm, Math.Round(value)))
            {
                OnPropertyChanged(nameof(RadiusDisplayText));
            }
        }
    }

    public string RadiusDisplayText => $"{RadiusKm:F0} km radius";

    public string CurrentLocationText
    {
        get => _currentLocationText;
        set => SetProperty(ref _currentLocationText, value);
    }

    public bool HasRequests
    {
        get => _hasRequests;
        set => SetProperty(ref _hasRequests, value);
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetProperty(ref _isRefreshing, value);
    }

    public ObservableCollection<TourRequestModel> Requests { get; } = new();

    public ICommand ToggleAvailabilityCommand { get; }
    public ICommand UpdateLocationCommand { get; }
    public ICommand RefreshRequestsCommand { get; }
    public ICommand AcceptRequestCommand { get; }
    public ICommand StartTourCommand { get; }
    public ICommand ApplyVolunteerCommand { get; }

    public VolunteerDashboardViewModel(IApiClient apiClient, IAuthService authService, LocationService locationService)
    {
        _apiClient = apiClient;
        _authService = authService;
        _locationService = locationService;
        Title = "Volunteer Dispatch";

        _authService.AuthStateChanged += () =>
        {
            OnPropertyChanged(nameof(IsVolunteer));
            OnPropertyChanged(nameof(IsNotVolunteer));
            OnPropertyChanged(nameof(VolunteerStatusText));
        };

        ToggleAvailabilityCommand = new Command(async () => await ToggleAvailabilityAsync());
        UpdateLocationCommand = new Command(async () => await SyncLocationAsync());
        RefreshRequestsCommand = new Command(async () => await LoadNearbyRequestsAsync());
        AcceptRequestCommand = new Command<TourRequestModel>(async (req) => await AcceptRequestAsync(req));
        StartTourCommand = new Command<TourRequestModel>(async (req) => await StartTourAsync(req));
        ApplyVolunteerCommand = new Command(async () => await ApplyVolunteerAsync());
    }

    public async Task InitializeAsync()
    {
        if (!_authService.IsAuthenticated) return;

        IsBusy = true;
        try
        {
            // Sync location first
            await SyncLocationAsync(silent: true);
            // Load requests
            await LoadNearbyRequestsAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ToggleAvailabilityAsync()
    {
        IsBusy = true;
        try
        {
            var updateDto = new
            {
                enabled = true,
                availableForRequests = IsAvailable,
                acceptsPrivateTours = true,
                acceptsGroupTours = true,
                maxConcurrentViewers = 10,
                languages = "English",
                tipEnabled = false
            };

            var success = await _apiClient.PutAsync("/api/v1/profile/volunteer-settings", updateDto);
            if (!success)
            {
                ErrorMessage = "Failed to update availability status on server.";
                // Revert toggle
                IsAvailable = !IsAvailable;
            }
            else
            {
                ErrorMessage = null;
                if (IsAvailable)
                {
                    await LoadNearbyRequestsAsync();
                }
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
            IsAvailable = !IsAvailable;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SyncLocationAsync(bool silent = false)
    {
        if (!silent) IsBusy = true;
        try
        {
            var location = await _locationService.GetCurrentLocationAsync();
            if (location != null)
            {
                _lastLatitude = location.Latitude;
                _lastLongitude = location.Longitude;
                _hasLocation = true;
                CurrentLocationText = $"GPS: {_lastLatitude:F4}, {_lastLongitude:F4}";

                var locDto = new
                {
                    latitude = _lastLatitude,
                    longitude = _lastLongitude
                };

                await _apiClient.PutAsync("/api/v1/profile/location", locDto);

                if (!silent)
                {
                    await LoadNearbyRequestsAsync();
                }
            }
            else
            {
                CurrentLocationText = "GPS: Unavailable (Check device permissions)";
            }
        }
        catch (Exception ex)
        {
            CurrentLocationText = $"GPS error: {ex.Message}";
        }
        finally
        {
            if (!silent) IsBusy = false;
        }
    }

    private async Task LoadNearbyRequestsAsync()
    {
        if (!IsVolunteer) return;

        IsRefreshing = true;
        ErrorMessage = null;

        try
        {
            var radiusMeters = RadiusKm * 1000;
            var endpoint = $"/api/v1/tour-requests?radius={radiusMeters}";
            var results = await _apiClient.GetAsync<TourRequestModel[]>(endpoint);

            Requests.Clear();
            if (results != null && results.Length > 0)
            {
                foreach (var r in results)
                {
                    Requests.Add(r);
                }
                HasRequests = true;
            }
            else
            {
                HasRequests = false;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to fetch incoming requests: {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async Task AcceptRequestAsync(TourRequestModel? request)
    {
        if (request == null) return;

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var success = await _apiClient.PostAsync<object>($"/api/v1/tour-requests/{request.Id}/accept", new { });
            if (success)
            {
                await Shell.Current.DisplayAlertAsync("Tour Accepted", 
                    $"You have accepted the tour for {request.PlaceName}! You can now start the live broadcast session whenever you are ready.", 
                    "Ready");
                await LoadNearbyRequestsAsync();
            }
            else
            {
                ErrorMessage = "Unable to accept tour request. It may have already been claimed or cancelled.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error accepting request: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task StartTourAsync(TourRequestModel? request)
    {
        if (request == null) return;

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var response = await _apiClient.PostAsync<object, StartTourResponseDto>($"/api/v1/tour-requests/{request.Id}/start", new { });
            if (response != null && response.Success && !string.IsNullOrEmpty(response.SessionId))
            {
                await Shell.Current.GoToAsync($"///LiveSessionPage?sessionId={Uri.EscapeDataString(response.SessionId)}");
            }
            else
            {
                ErrorMessage = "Failed to launch live tour session. Ensure you accepted this request first.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error starting tour session: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ApplyVolunteerAsync()
    {
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var result = await _apiClient.PostAsync<object, ApplyVolunteerResponseDto>("/api/v1/profile/volunteer/apply", new { });
            if (result != null)
            {
                await Shell.Current.DisplayAlertAsync("Volunteer Application", result.Message ?? "Application submitted for review.", "OK");
                await _authService.RefreshUserProfileAsync();
            }
            else
            {
                ErrorMessage = "Could not submit application. Ensure you have verified adult status (18+).";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error applying: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private class StartTourResponseDto
    {
        public bool Success { get; set; }
        public string? SessionId { get; set; }
    }

    private class ApplyVolunteerResponseDto
    {
        public string? Message { get; set; }
    }
}
