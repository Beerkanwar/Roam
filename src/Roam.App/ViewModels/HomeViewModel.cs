using System.Windows.Input;
using Roam.App.Services;

namespace Roam.App.ViewModels;

public class HomeViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private string _joinSessionId = string.Empty;

    public bool IsAuthenticated => _authService.IsAuthenticated;
    public bool IsNotAuthenticated => !_authService.IsAuthenticated;

    public string CurrentUserName => !string.IsNullOrEmpty(_authService.CurrentDisplayName)
        ? _authService.CurrentDisplayName
        : (!string.IsNullOrEmpty(_authService.CurrentUserEmail) ? _authService.CurrentUserEmail : "Guest");

    public bool IsVolunteer => _authService.IsVolunteer;

    public string VolunteerStatusText => _authService.VolunteerStatus switch
    {
        1 => "Application Pending",
        2 => "Approved Volunteer",
        3 => "Application Declined",
        4 => "Status Revoked",
        _ => "Not Registered"
    };

    public string JoinSessionId
    {
        get => _joinSessionId;
        set => SetProperty(ref _joinSessionId, value);
    }

    public ICommand LoginCommand { get; }
    public ICommand RegisterCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand JoinSessionCommand { get; }
    public ICommand ExploreCommand { get; }
    public ICommand VolunteerDashboardCommand { get; }

    public HomeViewModel(IAuthService authService)
    {
        _authService = authService;
        Title = "Roam — Exploration Platform";

        _authService.AuthStateChanged += OnAuthStateChanged;

        LoginCommand = new Command(async () => await Shell.Current.GoToAsync("LoginPage"));
        RegisterCommand = new Command(async () => await Shell.Current.GoToAsync("RegisterPage"));
        LogoutCommand = new Command(async () =>
        {
            await _authService.LogoutAsync();
            OnAuthStateChanged();
        });

        JoinSessionCommand = new Command(async () =>
        {
            if (string.IsNullOrWhiteSpace(JoinSessionId))
            {
                ErrorMessage = "Please enter a valid Live Session ID.";
                return;
            }

            ErrorMessage = null;
            await Shell.Current.GoToAsync($"///LiveSessionPage?sessionId={Uri.EscapeDataString(JoinSessionId.Trim())}");
        });

        ExploreCommand = new Command(async () =>
        {
            await Shell.Current.GoToAsync("///HomePage");
        });

        VolunteerDashboardCommand = new Command(async () =>
        {
            if (!IsVolunteer)
            {
                await Shell.Current.DisplayAlertAsync("Volunteer Program", 
                    "You must be an approved volunteer to access the dispatch dashboard. Apply from your profile.", "OK");
                return;
            }

            await Shell.Current.DisplayAlertAsync("Volunteer Dashboard", "Opening volunteer dispatch...", "OK");
        });
    }

    private void OnAuthStateChanged()
    {
        OnPropertyChanged(nameof(IsAuthenticated));
        OnPropertyChanged(nameof(IsNotAuthenticated));
        OnPropertyChanged(nameof(CurrentUserName));
        OnPropertyChanged(nameof(IsVolunteer));
        OnPropertyChanged(nameof(VolunteerStatusText));
    }
}
