using Microsoft.Extensions.Logging;
using ThuyetMinhTuDong.Data;
using CommunityToolkit.Maui;

namespace ThuyetMinhTuDong
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .UseMauiMaps()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            // Register LocalDatabase as a singleton
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "ThuyetMinhTuDong.db3");
            builder.Services.AddSingleton(s => ActivatorUtilities.CreateInstance<LocalDatabase>(s, dbPath));

            // Register Dependency Injection
            builder.Services.AddTransient<MainPage>();

            return builder.Build();
        }
    }
}
