using Microsoft.Extensions.Logging;
using Roam.App.Services;
using Roam.App.ViewModels;
using Roam.App.Views;

namespace Roam.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Register application services in DI container
        builder.Services.AddSingleton<LocationService>();
        builder.Services.AddSingleton<SignalRService>();
        builder.Services.AddSingleton<ApiClient>();
        builder.Services.AddSingleton<IApiClient>(sp => sp.GetRequiredService<ApiClient>());
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<IAuthService>(sp => sp.GetRequiredService<AuthService>());

        // Register ViewModels in DI container
        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<RegisterViewModel>();
        builder.Services.AddTransient<LiveSessionViewModel>();
        builder.Services.AddTransient<ExploreViewModel>();
        builder.Services.AddTransient<CreateRequestViewModel>();
        builder.Services.AddTransient<VolunteerDashboardViewModel>();

        // Register Pages in DI container
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<LiveSessionPage>();
        builder.Services.AddTransient<ExplorePage>();
        builder.Services.AddTransient<CreateRequestPage>();
        builder.Services.AddTransient<VolunteerDashboardPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
