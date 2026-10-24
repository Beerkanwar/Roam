using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Roam.App.Models;
using Roam.App.Services;

namespace Roam.App.ViewModels;

public class ExploreViewModel : BaseViewModel
{
    private readonly IApiClient _apiClient;
    private readonly IAuthService _authService;
    private string _searchQuery = string.Empty;
    private bool _hasResults;
    private bool _isCustomCoordsVisible;
    private string _customPlaceName = string.Empty;
    private string _customLatitude = string.Empty;
    private string _customLongitude = string.Empty;

    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value);
    }

    public ObservableCollection<PlaceModel> Places { get; } = new();

    public bool HasResults
    {
        get => _hasResults;
        set => SetProperty(ref _hasResults, value);
    }

    public bool IsCustomCoordsVisible
    {
        get => _isCustomCoordsVisible;
        set => SetProperty(ref _isCustomCoordsVisible, value);
    }

    public string CustomPlaceName
    {
        get => _customPlaceName;
        set => SetProperty(ref _customPlaceName, value);
    }

    public string CustomLatitude
    {
        get => _customLatitude;
        set => SetProperty(ref _customLatitude, value);
    }

    public string CustomLongitude
    {
        get => _customLongitude;
        set => SetProperty(ref _customLongitude, value);
    }

    public ICommand SearchCommand { get; }
    public ICommand QuickFilterCommand { get; }
    public ICommand ToggleCustomCoordsCommand { get; }
    public ICommand SelectPlaceCommand { get; }
    public ICommand RequestCustomCoordsCommand { get; }

    public ExploreViewModel(IApiClient apiClient, IAuthService authService)
    {
        _apiClient = apiClient;
        _authService = authService;
        Title = "Discover Destinations";

        SearchCommand = new Command(async () => await PerformSearchAsync(SearchQuery));
        QuickFilterCommand = new Command<string>(async (query) =>
        {
            if (string.IsNullOrWhiteSpace(query)) return;
            SearchQuery = query;
            await PerformSearchAsync(query);
        });

        ToggleCustomCoordsCommand = new Command(() =>
        {
            IsCustomCoordsVisible = !IsCustomCoordsVisible;
        });

        SelectPlaceCommand = new Command<PlaceModel>(async (place) =>
        {
            if (place == null) return;

            if (!_authService.IsAuthenticated)
            {
                await Shell.Current.DisplayAlertAsync("Authentication Required", "Please sign in to request a live exploration tour.", "OK");
                await Shell.Current.GoToAsync("LoginPage");
                return;
            }

            var route = $"CreateRequestPage?placeId={Uri.EscapeDataString(place.Id)}&placeName={Uri.EscapeDataString(place.Name)}&latitude={place.Latitude}&longitude={place.Longitude}";
            await Shell.Current.GoToAsync(route);
        });

        RequestCustomCoordsCommand = new Command(async () =>
        {
            if (!_authService.IsAuthenticated)
            {
                await Shell.Current.DisplayAlertAsync("Authentication Required", "Please sign in to request a live exploration tour.", "OK");
                await Shell.Current.GoToAsync("LoginPage");
                return;
            }

            if (!double.TryParse(CustomLatitude, out var lat) || lat < -90 || lat > 90)
            {
                ErrorMessage = "Please enter a valid latitude between -90 and 90.";
                return;
            }

            if (!double.TryParse(CustomLongitude, out var lon) || lon < -180 || lon > 180)
            {
                ErrorMessage = "Please enter a valid longitude between -180 and 180.";
                return;
            }

            var name = string.IsNullOrWhiteSpace(CustomPlaceName) ? $"Coordinates ({lat:F3}, {lon:F3})" : CustomPlaceName.Trim();
            ErrorMessage = null;

            var route = $"CreateRequestPage?placeName={Uri.EscapeDataString(name)}&latitude={lat}&longitude={lon}&isCoordinates=true";
            await Shell.Current.GoToAsync(route);
        });
    }

    public async Task PerformSearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            ErrorMessage = "Enter a search term or tap a discovery tag below.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var endpoint = $"/api/v1/places/search?q={Uri.EscapeDataString(query.Trim())}";
            var results = await _apiClient.GetAsync<PlaceModel[]>(endpoint, requireAuth: true);

            Places.Clear();
            if (results != null && results.Length > 0)
            {
                foreach (var p in results)
                {
                    Places.Add(p);
                }
                HasResults = true;
            }
            else
            {
                HasResults = false;
                ErrorMessage = $"No places found matching '{query}'. Try a landmark name, city, or custom coordinates.";
            }
        }
        catch (Exception ex)
        {
            HasResults = false;
            ErrorMessage = $"Unable to search destinations: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
