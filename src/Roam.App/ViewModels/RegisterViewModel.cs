using System;
using System.Windows.Input;
using Roam.App.Services;

namespace Roam.App.ViewModels;

public class RegisterViewModel : BaseViewModel
{
    private readonly IAuthService _authService;
    private string _email = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _displayName = string.Empty;
    private string _country = "United States";
    private DateTime _dateOfBirth = DateTime.Today.AddYears(-20);

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

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set => SetProperty(ref _confirmPassword, value);
    }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public string Country
    {
        get => _country;
        set => SetProperty(ref _country, value);
    }

    public DateTime DateOfBirth
    {
        get => _dateOfBirth;
        set => SetProperty(ref _dateOfBirth, value);
    }

    public ICommand RegisterCommand { get; }
    public ICommand NavigateToLoginCommand { get; }

    public RegisterViewModel(IAuthService authService)
    {
        _authService = authService;
        Title = "Create Account — Roam";

        RegisterCommand = new Command(async () => await ExecuteRegisterAsync(), () => !IsBusy);
        NavigateToLoginCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
    }

    private async Task ExecuteRegisterAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Email and password are required.";
            return;
        }

        if (Password != ConfirmPassword)
        {
            ErrorMessage = "Passwords do not match.";
            return;
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            ErrorMessage = "Please provide a public display name.";
            return;
        }

        // 18+ verification check
        var age = DateTime.Today.Year - DateOfBirth.Year;
        if (DateOfBirth > DateTime.Today.AddYears(-age)) age--;
        if (age < 18)
        {
            ErrorMessage = "You must be at least 18 years old to join Roam.";
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            var result = await _authService.RegisterAsync(Email, Password, DisplayName, Country, DateOfBirth);
            if (result.Success)
            {
                await Shell.Current.DisplayAlertAsync("Welcome to Roam", "Account successfully created!", "Get Started");
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Registration failed.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error during registration: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
