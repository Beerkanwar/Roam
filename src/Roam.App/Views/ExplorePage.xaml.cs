using Roam.App.ViewModels;

namespace Roam.App.Views;

public partial class ExplorePage : ContentPage
{
    public ExplorePage(ExploreViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
