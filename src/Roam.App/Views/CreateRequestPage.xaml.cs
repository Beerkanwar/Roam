using Roam.App.ViewModels;

namespace Roam.App.Views;

public partial class CreateRequestPage : ContentPage
{
    public CreateRequestPage(CreateRequestViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
