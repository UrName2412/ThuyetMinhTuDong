using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ThuyetMinhTuDong.Models;
using ThuyetMinhTuDong.Repositories;
using ThuyetMinhTuDong.Services;

namespace ThuyetMinhTuDong.ViewModels
{
    public class MainPageViewModel : INotifyPropertyChanged
    {
        private const bool EnableRemotePoiSync = true;
        private const string PoiApiPath = "/rest/v1/poi?select=*";
        private const string DefaultSupabaseHost = "https://vkicutmxykziwygemslh.supabase.co";
        private const string ApiHostPreferenceKey = "poi_api_host";
        private const string SupabaseAnonKeyPreferenceKey = "supabase_anon_key";
        private const string DefaultSupabaseAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InZraWN1dG14eWt6aXd5Z2Vtc2xoIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NzU0MTc1NDAsImV4cCI6MjA5MDk5MzU0MH0.SVNFu7wpI-TTLRXDvAOX_KPRXIvX7TEQapi0DjNX2z0";

        private readonly IPoiRepository _poiRepository;
        private readonly TTSService _ttsService;
        private readonly LocationService _locationService;

        private bool _isLanguageInitialized;
        private string _currentDescriptionVietnamese = string.Empty;
        private string _currentMapLink = string.Empty;
        private string _languageButtonText = "Tiếng Việt ▾";
        private string _nearbyStatusText = "Hiện tại không có mục nào để hiển thị.";
        private bool _isNearbyStatusVisible = true;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? PlayStarted;
        public event EventHandler? PlayStopped;
        public event EventHandler<string>? PermissionDenied;
        public event EventHandler<string>? TranslationStarted;
        public event EventHandler? TranslationFinished;


        public ObservableCollection<PointOfInterest> NearbyPois { get; } = new();

        // Danh sách ảnh cho POI đang chọn
        private ObservableCollection<Models.Image> _poiImages = new();
        public ObservableCollection<Models.Image> PoiImages
        {
            get => _poiImages;
            set => SetProperty(ref _poiImages, value);
        }

        // Lấy ảnh từ Supabase cho POI
        public async Task LoadImagesForPoiAsync(long poiId, string? poiName = null)
        {
            var images = await _poiRepository.GetImagesByPoiIdAsync(poiId, poiName);
            PoiImages = new ObservableCollection<Models.Image>(images);
        }

        public async Task<string?> GetFirstImageUrlForPoiAsync(long poiId, string? poiName = null)
        {
            var images = await _poiRepository.GetImagesByPoiIdAsync(poiId, poiName);
            return images.FirstOrDefault()?.ImageUrl;
        }

        public string CurrentDescriptionVietnamese
        {
            get => _currentDescriptionVietnamese;
            set => SetProperty(ref _currentDescriptionVietnamese, value);
        }

        public string CurrentMapLink
        {
            get => _currentMapLink;
            set
            {
                SetProperty(ref _currentMapLink, value);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMapLinkVisible)));
            }
        }

        public bool IsMapLinkVisible => !string.IsNullOrEmpty(_currentMapLink);

        public string LanguageButtonText
        {
            get => _languageButtonText;
            set => SetProperty(ref _languageButtonText, value);
        }

        public string NearbyStatusText
        {
            get => _nearbyStatusText;
            set => SetProperty(ref _nearbyStatusText, value);
        }

        public bool IsNearbyStatusVisible
        {
            get => _isNearbyStatusVisible;
            set => SetProperty(ref _isNearbyStatusVisible, value);
        }

        public bool IsPlaying => _ttsService.IsPlaying;
        public IEnumerable<Locale> AvailableLocales => _ttsService.AvailableLocales;
        public string SelectedLanguageCode => _ttsService.SelectedLanguageCode;

        public MainPageViewModel(IPoiRepository poiRepository, TTSService ttsService, LocationService locationService)
        {
            _poiRepository = poiRepository;
            _ttsService = ttsService;
            _locationService = locationService;

            _ttsService.PlayStarted += (s, e) => PlayStarted?.Invoke(this, EventArgs.Empty);
            _ttsService.PlayStopped += (s, e) => PlayStopped?.Invoke(this, EventArgs.Empty);
            _ttsService.TranslationStarted += (s, targetLangName) => TranslationStarted?.Invoke(this, targetLangName);
            _ttsService.TranslationFinished += (s, e) => TranslationFinished?.Invoke(this, EventArgs.Empty);
            _locationService.PermissionDenied += (s, e) => PermissionDenied?.Invoke(this, e.Message);
        }

        public async Task InitializeAsync()
        {
            try
            {
                await _poiRepository.DeleteEmptyNamePOIsAsync();
            }
            catch
            {
            }

            await _ttsService.InitializeAsync();

            if (!_isLanguageInitialized)
            {
                var deviceLanguage = System.Globalization.CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
                if (_ttsService.AvailableLocales?.Any() == true)
                {
                    var matchedLocale = _ttsService.AvailableLocales.FirstOrDefault(l =>
                        l.Language.StartsWith(deviceLanguage, StringComparison.OrdinalIgnoreCase));

                    if (matchedLocale != null)
                    {
                        _ttsService.SetLanguage(matchedLocale.Language);
                        string displayName = _ttsService.GetLanguageDisplayName(matchedLocale.Language);
                        LanguageButtonText = $"{displayName} ▾";
                    }
                }

                _isLanguageInitialized = true;
            }

            var lastSync = await _poiRepository.GetLastSyncTimeAsync("poi_last_sync");
            var hoursSinceSync = lastSync.HasValue
                ? (DateTime.Now - lastSync.Value).TotalHours
                : 24;

            if (EnableRemotePoiSync && hoursSinceSync > 12)
            {
                var poiApiUrl = GetPoiApiUrl();
                var supabaseAnonKey = GetSupabaseAnonKey();
                bool synced = await _poiRepository.SyncPOIsFromApiAsync(poiApiUrl, supabaseAnonKey);

                if (synced)
                {
                    await _poiRepository.CleanupSoftDeletedPOIsAsync(daysOld: 90);
                }
            }
        }

        public Task<bool> CheckAndRequestLocationPermissionAsync()
            => _locationService.CheckAndRequestPermissionAsync();

        public Task<Location?> GetCurrentLocationAsync()
            => _locationService.GetCurrentLocationAsync();

        public Microsoft.Maui.Maps.MapSpan? CreateMapSpan(Location location)
            => _locationService.CreateMapSpan(location);

        public async Task LoadNearbyPoisAsync(Location userLocation, bool syncApi = true)
        {
            NearbyPois.Clear();
            NearbyStatusText = "Đang tải địa điểm...";
            IsNearbyStatusVisible = true;

            bool synced = false;
            if (syncApi && EnableRemotePoiSync)
            {
                var poiApiUrl = GetPoiApiUrl();
                var supabaseAnonKey = GetSupabaseAnonKey();
                synced = await _poiRepository.SyncPOIsFromApiAsync(poiApiUrl, supabaseAnonKey);
            }

            if (!synced)
            {
                await _poiRepository.EnsureDefaultPOIsAsync(userLocation);
            }

            var pois = await _poiRepository.GetNearbyActivePOIsAsync(userLocation, radiusKm: 2, forceRefresh: true);
            foreach (var poi in pois)
            {
                NearbyPois.Add(poi);
            }

            NearbyStatusText = "Không có địa điểm nào trong bán kính 2 km.";
            IsNearbyStatusVisible = NearbyPois.Count == 0;
        }

        public Microsoft.Maui.Controls.Maps.Pin? CreateMapPin(PointOfInterest poi)
            => _poiRepository.CreateMapPin(poi);

        public async Task HandleLanguageSelectionAsync(string languageCode, string displayName)
        {
            _ttsService.SetLanguage(languageCode);
            string buttonText = !string.IsNullOrEmpty(displayName) ? displayName : languageCode;
            LanguageButtonText = $"{buttonText} ▾";
            await Task.CompletedTask;
        }

        public void SetLanguage(string languageCode)
            => _ttsService.SetLanguage(languageCode);

        public async Task SpeakAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            await _ttsService.SpeakAsync(text);
        }

        public List<Locale> GetVoicesForLanguage(string languageCode)
            => _ttsService.GetVoicesForLanguage(languageCode);

        public void SetVoice(Locale locale)
            => _ttsService.SetVoice(locale);

        public void StopSpeaking()
            => _ttsService.StopSpeaking();

        public async Task TogglePlayPauseAsync(bool isTtsEnabled, string? fallbackDescription)
        {
            if (!isTtsEnabled)
                return;

            if (_ttsService.IsPlaying)
            {
                _ttsService.StopSpeaking();
                return;
            }

            var textToSpeak = !string.IsNullOrWhiteSpace(CurrentDescriptionVietnamese)
                ? CurrentDescriptionVietnamese
                : fallbackDescription;

            if (!string.IsNullOrWhiteSpace(textToSpeak))
            {
                await _ttsService.SpeakAsync(textToSpeak);
            }
        }

        public async Task AutoSpeakAsync(bool isTtsEnabled, string description)
        {
            if (!isTtsEnabled || _ttsService.IsPlaying || string.IsNullOrWhiteSpace(description))
                return;

            await Task.Delay(100);
            await _ttsService.SpeakAsync(description);
        }

        public Task<List<PointOfInterest>> GetNearbyPoisFromCacheAsync(Location userLocation, double radiusKm)
            => _poiRepository.GetNearbyActivePOIsAsync(userLocation, radiusKm, forceRefresh: false);

        public Task<List<PointOfInterest>> GetAllActivePoisFromCacheAsync()
            => _poiRepository.GetAllActivePOIsAsync(forceRefresh: false);

        private string GetConfiguredSupabaseHost()
        {
            var configuredHost = Preferences.Default.Get(ApiHostPreferenceKey, DefaultSupabaseHost)?.Trim();

            if (string.IsNullOrWhiteSpace(configuredHost))
            {
                configuredHost = DefaultSupabaseHost;
            }

            if (!configuredHost.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !configuredHost.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                configuredHost = $"https://{configuredHost}";
            }

            return configuredHost.TrimEnd('/');
        }

        private string GetSupabaseAnonKey()
        {
            var configuredKey = Preferences.Default.Get(SupabaseAnonKeyPreferenceKey, DefaultSupabaseAnonKey)?.Trim();
            return configuredKey ?? string.Empty;
        }

        private string GetPoiApiUrl()
        {
            var configuredHost = GetConfiguredSupabaseHost();
            return string.IsNullOrWhiteSpace(configuredHost)
                ? string.Empty
                : $"{configuredHost}{PoiApiPath}";
        }

        private void SetProperty<T>(ref T backingField, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(backingField, value))
                return;

            backingField = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
