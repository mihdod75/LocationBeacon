using LocationBeacon.Services;
using Microsoft.Extensions.Logging;

namespace LocationBeacon;

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
            })
            .Services
            .AddSingleton<BeaconService>()
            .AddSingleton<LocationService>()
            .AddSingleton<PreferencesService>()
            .AddSingleton<DeviceInfoService>()
            .AddSingleton<EnrollmentService>()
            .AddSingleton<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}