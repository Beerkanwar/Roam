using Microsoft.Maui.Controls;
using Roam.App.ViewModels;

namespace Roam.App.Views;

public partial class LiveSessionPage : ContentPage
{
    public LiveSessionPage()
    {
        InitializeComponent();
    }

    public LiveSessionPage(LiveSessionViewModel viewModel) : this()
    {
        BindingContext = viewModel;
    }
}
