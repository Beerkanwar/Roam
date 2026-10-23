using Microsoft.Extensions.Logging;
using Roam.App.Services;
using Roam.App.ViewModels;
using Roam.App.Views;

namespace Roam.App {
    public static class MauiProgram {
        public static MauiApp CreateMauiApp() {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts => {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // Register application services and view models in DI container
            builder.Services.AddSingleton<LocationService>();
            builder.Services.AddSingleton<SignalRService>();
            builder.Services.AddTransient<LiveSessionViewModel>();
            builder.Services.AddTransient<LiveSessionPage>();

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
