using Microsoft.Extensions.Logging;
using ThuyetMinhTuDong.Data;
using CommunityToolkit.Maui;
using ThuyetMinhTuDong.Repositories;
using ThuyetMinhTuDong.Services;
using ThuyetMinhTuDong.ViewModels;
using ZXing.Net.Maui.Controls;

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
                .UseBarcodeReader()
                .UseMauiMaps()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("fa-solid-900.ttf", "FontAwesomeSolid");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            // Register LocalDatabase as a singleton
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "ThuyetMinhTuDong.db3");
            builder.Services.AddSingleton(s => ActivatorUtilities.CreateInstance<LocalDatabase>(s, dbPath));

            builder.Services.AddSingleton<PlaceService>();
            builder.Services.AddSingleton<LocationService>();
            builder.Services.AddSingleton<StatusService>();
            builder.Services.AddSingleton<OnlinePresenceService>();
            builder.Services.AddSingleton<UserService>();
            builder.Services.AddSingleton<ITranslateService, TranslateService>();
            builder.Services.AddSingleton<TTSService>();
            builder.Services.AddSingleton<IPoiRepository, PoiRepository>();

            // Register ViewModels and Pages
            builder.Services.AddTransient<MainPageViewModel>();
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<QrScannerPage>();

            return builder.Build();
        }
    }
}
