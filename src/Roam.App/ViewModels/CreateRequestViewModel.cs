using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using Roam.App.Services;

namespace Roam.App.ViewModels;

public class CreateRequestViewModel : BaseViewModel, IQueryAttributable
{
    private readonly IApiClient _apiClient;
    private string _placeId = string.Empty;
    private string _placeName = "Destination Exploration";
    private double _latitude;
    private double _longitude;
    private bool _isCoordinatesMode;
    private string _description = string.Empty;
    private bool _isPrivate = true; // Default 1-on-1 private
    private bool _isInstant = true; // Default Instant live exploration

    public string PlaceId
    {
        get => _placeId;
        set => SetProperty(ref _placeId, value);
    }

    public string PlaceName
    {
        get => _placeName;
        set => SetProperty(ref _placeName, value);
    }

    public double Latitude
    {
        get => _latitude;
        set => SetProperty(ref _latitude, value);
    }

    public double Longitude
    {
        get => _longitude;
        set => SetProperty(ref _longitude, value);
    }

    public bool IsCoordinatesMode
    {
        get => _isCoordinatesMode;
        set => SetProperty(ref _isCoordinatesMode, value);
    }

    public string Description
    {
        get => _description;
        set
        {
            SetProperty(ref _description, value);
            OnPropertyChanged(nameof(CharCountText));
        }
    }

    public string CharCountText => $"{Description?.Length ?? 0} / 2000";

    public bool IsPrivate
    {
        get => _isPrivate;
        set
        {
            if (SetProperty(ref _isPrivate, value))
            {
                OnPropertyChanged(nameof(IsGroup));
            }
        }
    }

    public bool IsGroup
    {
        get => !_isPrivate;
        set => IsPrivate = !value;
    }

    public bool IsInstant
    {
        get => _isInstant;
        set
        {
            if (SetProperty(ref _isInstant, value))
            {
                OnPropertyChanged(nameof(IsScheduled));
            }
        }
    }

    public bool IsScheduled
    {
        get => !_isInstant;
        set => IsInstant = !value;
    }

    public string LocationFormatted => $"{Latitude:F4}, {Longitude:F4}";

    public ICommand SelectPrivateCommand { get; }
    public ICommand SelectGroupCommand { get; }
    public ICommand SelectInstantCommand { get; }
    public ICommand SelectScheduledCommand { get; }
    public ICommand SubmitRequestCommand { get; }
    public ICommand CancelCommand { get; }

    public CreateRequestViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;
        Title = "Request Live Tour";

        SelectPrivateCommand = new Command(() => IsPrivate = true);
        SelectGroupCommand = new Command(() => IsPrivate = false);
        SelectInstantCommand = new Command(() => IsInstant = true);
        SelectScheduledCommand = new Command(() => IsInstant = false);

        CancelCommand = new Command(async () => await Shell.Current.GoToAsync(".."));

        SubmitRequestCommand = new Command(async () => await SubmitAsync());
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("placeId", out var pid) && pid is string placeId)
        {
            PlaceId = placeId;
        }

        if (query.TryGetValue("placeName", out var pname) && pname is string placeName)
        {
            PlaceName = Uri.UnescapeDataString(placeName);
        }

        if (query.TryGetValue("latitude", out var latStr) && double.TryParse(latStr?.ToString(), out var lat))
        {
            Latitude = lat;
        }

        if (query.TryGetValue("longitude", out var lonStr) && double.TryParse(lonStr?.ToString(), out var lon))
        {
            Longitude = lon;
        }

        if (query.TryGetValue("isCoordinates", out var isCoords))
        {
            IsCoordinatesMode = isCoords?.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;
        }
        else
        {
            IsCoordinatesMode = string.IsNullOrEmpty(PlaceId);
        }

        OnPropertyChanged(nameof(LocationFormatted));
    }

    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(Description) || Description.Length < 5)
        {
            ErrorMessage = "Please describe what you would like to explore (minimum 5 characters).";
            return;
        }

        if (Description.Length > 2000)
        {
            ErrorMessage = "Description must not exceed 2000 characters.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var visibility = IsPrivate ? 0 : 1; // 0: Private, 1: Group
            var mode = IsInstant ? 0 : 1;       // 0: Instant, 1: Scheduled

            bool success;

            if (IsCoordinatesMode || string.IsNullOrEmpty(PlaceId))
            {
                var body = new
                {
                    latitude = Latitude,
                    longitude = Longitude,
                    visibility,
                    mode,
                    description = Description.Trim()
                };

                var response = await _apiClient.PostAsync<object, CreateRequestResponseDto>("/api/v1/tour-requests/coordinates", body);
                success = response != null && !string.IsNullOrEmpty(response.Id);
            }
            else
            {
                var body = new
                {
                    placeId = PlaceId,
                    visibility,
                    mode,
                    description = Description.Trim()
                };

                var response = await _apiClient.PostAsync<object, CreateRequestResponseDto>("/api/v1/tour-requests", body);
                success = response != null && !string.IsNullOrEmpty(response.Id);
            }

            if (success)
            {
                await Shell.Current.DisplayAlertAsync("Tour Request Broadcasted", 
                    $"Your request for '{PlaceName}' has been sent to nearby verified volunteers. You will be notified as soon as a volunteer accepts!", 
                    "Great");
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                ErrorMessage = "Failed to broadcast tour request. Please ensure you are age-verified and try again.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error submitting request: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private class CreateRequestResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
