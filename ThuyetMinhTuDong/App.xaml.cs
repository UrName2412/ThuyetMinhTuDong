using ThuyetMinhTuDong.Services;

namespace ThuyetMinhTuDong
{
    public partial class App : Application
    {
        private readonly OnlinePresenceService _onlinePresenceService;

        public App(OnlinePresenceService onlinePresenceService)
        {
            InitializeComponent();
            _onlinePresenceService = onlinePresenceService;

            _ = MainThread.InvokeOnMainThreadAsync(async () => await _onlinePresenceService.StartAsync());
        }

        protected override void OnSleep()
        {
            base.OnSleep();
            _ = MainThread.InvokeOnMainThreadAsync(async () => await _onlinePresenceService.StopAsync());
        }

        protected override void OnResume()
        {
            base.OnResume();
            _ = MainThread.InvokeOnMainThreadAsync(async () => await _onlinePresenceService.StartAsync());
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}