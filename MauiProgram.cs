using HeatMapTracker.Services;
using HeatMapTracker.Views;
using Microsoft.Extensions.Logging;

namespace HeatMapTracker;

/// <summary>
/// Application entry point. Registers the Maps handlers and wires up dependency injection.
/// </summary>
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        MauiAppBuilder builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiMaps()   // Required for Microsoft.Maui.Controls.Maps to resolve handlers.
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // The database and tracker hold state for the lifetime of the app, so both are singletons.
        builder.Services.AddSingleton<LocationDatabase>();
        builder.Services.AddSingleton<LocationTrackingService>();
        builder.Services.AddSingleton<MainView>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
