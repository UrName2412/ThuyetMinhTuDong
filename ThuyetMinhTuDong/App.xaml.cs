using ThuyetMinhTuDong.Services;

namespace ThuyetMinhTuDong
{
    public partial class App : Application
    {
        private readonly OnlinePresenceService _onlinePresenceService;
        private readonly UserService _userService;

        public App(OnlinePresenceService onlinePresenceService, UserService userService)
        {
            InitializeComponent();
            _onlinePresenceService = onlinePresenceService;
            _userService = userService;

            _ = MainThread.InvokeOnMainThreadAsync(async () => await _onlinePresenceService.StartAsync());
        }

        protected override void OnStart()
        {
            base.OnStart();
            _ = Task.Run(async () => await _userService.RegisterUserAsync());
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