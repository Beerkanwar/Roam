using System.Windows.Input;
using Roam.App.Services;

namespace Roam.App.ViewModels;

public class LoginViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private string _email = string.Empty;
    private string _password = string.Empty;

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public ICommand LoginCommand { get; }
    public ICommand NavigateToRegisterCommand { get; }

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
        Title = "Sign In — Roam";

        LoginCommand = new Command(async () => await ExecuteLoginAsync(), () => !IsBusy);
        NavigateToRegisterCommand = new Command(async () => await Shell.Current.GoToAsync("RegisterPage"));
    }

    private async Task ExecuteLoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter both email and password.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var success = await _authService.LoginAsync(Email, Password);
            if (success)
            {
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                ErrorMessage = "Invalid credentials or account suspended.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Sign in failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
