using Microsoft.Maui.Controls;
using Roam.App.ViewModels;

namespace Roam.App.Views;

public partial class LiveSessionPage : ContentPage
{
    public LiveSessionPage(LiveSessionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
