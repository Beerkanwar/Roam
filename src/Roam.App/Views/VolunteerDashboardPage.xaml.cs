using Roam.App.ViewModels;

namespace Roam.App.Views;

public partial class VolunteerDashboardPage : ContentPage
{
    private readonly VolunteerDashboardViewModel _viewModel;

    public VolunteerDashboardPage(VolunteerDashboardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }
}
