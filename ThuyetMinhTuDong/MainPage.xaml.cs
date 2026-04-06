using ThuyetMinhTuDong.Data;
using ThuyetMinhTuDong.Models;
using ThuyetMinhTuDong.Services;
using Microsoft.Maui.Controls.Compatibility;
using Microsoft.Maui.Storage;

namespace ThuyetMinhTuDong
{
    [QueryProperty(nameof(SelectedLanguageCode), "selectedLanguageCode")]
    [QueryProperty(nameof(SelectedLanguageDisplay), "selectedLanguageDisplay")]
    public partial class MainPage : ContentPage
    {
        private const string PoiApiPath = "/rest/v1/poi?select=*";
        private const string DefaultSupabaseHost = "https://vkicutmxykziwygemslh.supabase.co";
        private const string ApiHostPreferenceKey = "poi_api_host";
        private const string SupabaseAnonKeyPreferenceKey = "supabase_anon_key";
        private const string DefaultSupabaseAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InZraWN1dG14eWt6aXd5Z2Vtc2xoIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NzU0MTc1NDAsImV4cCI6MjA5MDk5MzU0MH0.SVNFu7wpI-TTLRXDvAOX_KPRXIvX7TEQapi0DjNX2z0";
        private const bool EnableRemotePoiSync = false;
        private const string PlayIconGlyph = "\uf04b";
        private const string PauseIconGlyph = "\uf04c";

        private readonly LocalDatabase _database;
        private readonly TTSService _ttsService;
        private readonly LocationService _locationService;
        private readonly PlaceService _placeService;

        private string _selectedLanguageCodeParam;
        private string _selectedLanguageDisplayParam;
        private bool isExpanded = false;
        private string _currentDescriptionVietnamese = string.Empty;
        private bool _isLanguageInitialized = false;

        public string SelectedLanguageCode
        {
            get => _selectedLanguageCodeParam;
            set
            {
                _selectedLanguageCodeParam = value;
                TryHandleLanguageSelection();
            }
        }

        public string SelectedLanguageDisplay
        {
            get => _selectedLanguageDisplayParam;
            set
            {
                _selectedLanguageDisplayParam = value;
                TryHandleLanguageSelection();
            }
        }

        private void TryHandleLanguageSelection()
        {
            // Chỉ gọi HandleLanguageSelection khi cả hai giá trị đã được set
            if (!string.IsNullOrEmpty(_selectedLanguageCodeParam) && !string.IsNullOrEmpty(_selectedLanguageDisplayParam))
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await HandleLanguageSelection(_selectedLanguageCodeParam, _selectedLanguageDisplayParam);
                    _selectedLanguageCodeParam = null;
                    _selectedLanguageDisplayParam = null;
                });
            }
        }

        public MainPage(LocalDatabase database)
        {
            InitializeComponent();
            _database = database;

            // Khởi tạo các service
            _ttsService = new TTSService(database);
            _locationService = new LocationService();
            _placeService = new PlaceService(database);

            // Đăng ký sự kiện từ service
            SubscribeToServiceEvents();
        }

        private void SubscribeToServiceEvents()
        {
            _ttsService.PlayStarted += (s, e) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var playPauseIcon = this.FindByName<Label>("PlayPauseIcon");
                    if (playPauseIcon != null) playPauseIcon.Text = PauseIconGlyph;
                });
            };

            _ttsService.PlayStopped += (s, e) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var playPauseIcon = this.FindByName<Label>("PlayPauseIcon");
                    if (playPauseIcon != null) playPauseIcon.Text = PlayIconGlyph;
                });
            };

            _locationService.PermissionDenied += async (s, e) =>
            {
                await DisplayAlert("Quyền bị từ chối", e.Message, "OK");
            };
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            Task.Run(async () => 
            {
                try
                {
                    // DỌN DẸP: Xóa các POI không có tên (không chặn luồng chính)
                    try
                    {
                        await _database.DeleteEmptyNamePOIsAsync();
                    }
                    catch (Exception)
                    {
                    }

                    // Khởi tạo dịch vụ TTS
                    await _ttsService.InitializeAsync();

                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        try
                        {
                            if (!_isLanguageInitialized)
                            {
                                // Đặt ngôn ngữ mặc định theo ngôn ngữ hệ thống của thiết bị
                                var deviceLanguage = System.Globalization.CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
                                if (_ttsService.AvailableLocales?.Any() == true)
                                {
                                    var matchedLocale = _ttsService.AvailableLocales.FirstOrDefault(l => 
                                        l.Language.StartsWith(deviceLanguage, StringComparison.OrdinalIgnoreCase));
                                    
                                    if (matchedLocale != null)
                                    {
                                        _ttsService.SetLanguage(matchedLocale.Language);
                                        var languageButton = this.FindByName<Button>("LanguageButton");
                                        if (languageButton != null)
                                        {
                                            string displayName = _ttsService.GetLanguageDisplayName(matchedLocale.Language);
                                            languageButton.Text = $"{displayName} ▾";
                                        }
                                    }
                                }
                                _isLanguageInitialized = true;
                            }

                            // Kiểm tra thời gian đồng bộ lần cuối và tự động đồng bộ nếu cần
                            var lastSync = await _database.GetLastSyncTimeAsync("poi_last_sync");
                            var hoursSinceSync = lastSync.HasValue 
                                ? (DateTime.Now - lastSync.Value).TotalHours 
                                : 24;

                            if (hoursSinceSync > 12)
                            {
                                var poiApiUrl = GetPoiApiUrl();
                                var supabaseAnonKey = GetSupabaseAnonKey();
                                bool synced = await _placeService.SyncPOIsFromApiAsync(poiApiUrl, supabaseAnonKey);

                                if (synced)
                                {
                                    // Dọn dẹp các POI đã xóa mềm quá cũ (>90 ngày)
                                    await _placeService.CleanupSoftDeletedPOIsAsync(daysOld: 90);
                                }
                            }

                            // Yêu cầu quyền vị trí và định vị
                            await CheckAndRequestLocationPermission();

                            // Bật hiển thị vị trí trên bản đồ
                            if (this.FindByName<Microsoft.Maui.Controls.Maps.Map>("MyMap") is { } map)
                            {
                                map.IsShowingUser = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            await DisplayAlert("Lỗi", $"Lỗi khởi tạo: {ex.Message}", "OK");
                        }
                    });
                }
                catch (Exception)
                {
                }
            });
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            // Tối ưu: Tắt định vị khi rời khỏi trang để tiết kiệm pin
            if (this.FindByName<Microsoft.Maui.Controls.Maps.Map>("MyMap") is { } map)
            {
                map.IsShowingUser = false;
            }
        }

        private async Task CheckAndRequestLocationPermission()
        {
            try
            {
                bool permissionGranted = await _locationService.CheckAndRequestPermissionAsync();

                if (permissionGranted)
                {
                    var location = await _locationService.GetCurrentLocationAsync();

                    if (location != null)
                    {
                        if (this.FindByName<Microsoft.Maui.Controls.Maps.Map>("MyMap") is { } map)
                        {
                            map.IsShowingUser = true;

                            var mapSpan = _locationService.CreateMapSpan(location);
                            map.MoveToRegion(mapSpan);

                            await AddPOIsToMapAsync(map, location);
                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }

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

        private async Task AddPOIsToMapAsync(Microsoft.Maui.Controls.Maps.Map map, Location userLocation)
        {
            map.Pins.Clear();

            var poiApiUrl = GetPoiApiUrl();
            var supabaseAnonKey = GetSupabaseAnonKey();
            bool synced = await _placeService.SyncPOIsFromApiAsync(poiApiUrl, supabaseAnonKey);

            if (!synced)
            {
                await _placeService.EnsureDefaultPOIsAsync(userLocation);
            }

            // Lấy các POI đang hoạt động (chưa bị xóa mềm)
            var pois = await _placeService.GetAllActivePOIsAsync(forceRefresh: true);

            var nearbyList = this.FindByName<VerticalStackLayout>("NearbyPlacesList");
            var emptyLabel = this.FindByName<Label>("EmptyNearbyLabel");

            if (emptyLabel != null && pois.Count > 0)
            {
                emptyLabel.IsVisible = false;
            }

            if (nearbyList != null)
            {
                nearbyList.Children.Clear();
            }

            foreach (var poi in pois)
            {
                var pin = _placeService.CreateMapPin(poi);
                if (pin != null)
                {
                    pin.MarkerClicked += (s, args) =>
                    {
                        OnPlaceSelected(poi.Name, poi.Description, "Bản đồ");
                    };

                    map.Pins.Add(pin);
                }

                if (nearbyList != null)
                {
                    CreateAndAddNearbyPlaceItem(nearbyList, poi);
                }
            }
        }

        private void CreateAndAddNearbyPlaceItem(VerticalStackLayout nearbyList, PointOfInterest poi)
        {
            var itemLayout = new Border
            {
                BackgroundColor = Color.FromArgb("#1A1111"),
                Stroke = Color.FromArgb("#B71C1C"),
                Padding = 15,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 }
            };

            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += (s, e) =>
            {
                OnPlaceSelected(poi.Name, poi.Description, "Gần bạn");
            };
            itemLayout.GestureRecognizers.Add(tapGesture);

            var contentLayout = new VerticalStackLayout { Spacing = 5 };
            contentLayout.Children.Add(new Label
            {
                Text = poi.Name,
                TextColor = Colors.White,
                FontAttributes = FontAttributes.Bold,
                FontSize = 16
            });
            contentLayout.Children.Add(new Label
            {
                Text = poi.Description,
                TextColor = Color.FromArgb("#A0938A"),
                FontSize = 13,
                MaxLines = 4,
                LineBreakMode = LineBreakMode.TailTruncation
            });

            itemLayout.Content = contentLayout;
            nearbyList.Children.Add(itemLayout);
        }

        private async void OnSwipeUp(object sender, SwipedEventArgs e)
        {
            if (isExpanded) return;
            isExpanded = true;

            var expandableContent = this.FindByName<VerticalStackLayout>("ExpandableContent");
            var coordBorder = this.FindByName<Border>("CoordBorder");

            if (expandableContent != null)
            {
                expandableContent.Opacity = 0;

                Animation parentAnimation = new Animation();

                var heightAnimation = new Animation(v => expandableContent.HeightRequest = v, 0, 400, Easing.CubicOut);
                var fadeAnimation = new Animation(v => expandableContent.Opacity = v, 0, 1, Easing.CubicIn);

                parentAnimation.Add(0, 1, heightAnimation);
                parentAnimation.Add(0.5, 1, fadeAnimation);

                parentAnimation.Commit(this, "ExpandDrawer", 16, 300);

                if (coordBorder != null)
                {
                    await coordBorder.TranslateTo(0, -320, 300, Easing.CubicOut);
                }
            }
        }

        private async void OnSwipeDown(object sender, SwipedEventArgs e)
        {
            if (!isExpanded) return;
            isExpanded = false;

            var expandableContent = this.FindByName<VerticalStackLayout>("ExpandableContent");
            var coordBorder = this.FindByName<Border>("CoordBorder");

            if (expandableContent != null)
            {
                Animation parentAnimation = new Animation();

                var heightAnimation = new Animation(v => expandableContent.HeightRequest = v, 400, 0, Easing.CubicIn);
                var fadeAnimation = new Animation(v => expandableContent.Opacity = v, 1, 0, Easing.CubicOut);

                parentAnimation.Add(0, 1, heightAnimation);
                parentAnimation.Add(0, 0.5, fadeAnimation);

                parentAnimation.Commit(this, "CollapseDrawer", 16, 300);

                if (coordBorder != null)
                {
                    await coordBorder.TranslateTo(0, 0, 300, Easing.CubicIn);
                }
            }
        }

        private void OnTab1Tapped(object sender, EventArgs e)
        {
            UpdateTabVisuals(1);
        }

        private void OnTab2Tapped(object sender, EventArgs e)
        {
            UpdateTabVisuals(2);
        }

        private void OnTab3Tapped(object sender, EventArgs e)
        {
            UpdateTabVisuals(3);
        }

        private void UpdateTabVisuals(int selectedTabIndex)
        {
            var lblTab1 = this.FindByName<Label>("LblTab1");
            var lineTab1 = this.FindByName<BoxView>("LineTab1");
            var lblTab2 = this.FindByName<Label>("LblTab2");
            var lineTab2 = this.FindByName<BoxView>("LineTab2");
            var lblTab3 = this.FindByName<Label>("LblTab3");
            var lineTab3 = this.FindByName<BoxView>("LineTab3");

            var tab1Content = this.FindByName<View>("Tab1Content");
            var tab2Content = this.FindByName<View>("Tab2Content");
            var tab3Content = this.FindByName<View>("Tab3Content");

            // Đặt lại hiển thị cho tất cả tiêu đề tab
            if(lblTab1 != null) lblTab1.TextColor = Color.FromArgb("#A0938A");
            if(lineTab1 != null) lineTab1.Color = Color.FromArgb("#3A2121");
            if(lblTab2 != null) lblTab2.TextColor = Color.FromArgb("#A0938A");
            if(lineTab2 != null) lineTab2.Color = Color.FromArgb("#3A2121");
            if(lblTab3 != null) lblTab3.TextColor = Color.FromArgb("#A0938A");
            if(lineTab3 != null) lineTab3.Color = Color.FromArgb("#3A2121");

            // Ẩn tất cả nội dung Tab
            if(tab1Content != null) tab1Content.IsVisible = false;
            if(tab2Content != null) tab2Content.IsVisible = false;
            if(tab3Content != null) tab3Content.IsVisible = false;

            // Kích hoạt tab được chọn
            if (selectedTabIndex == 1)
            {
                if(lblTab1 != null) lblTab1.TextColor = Color.FromArgb("#D4AF37");
                if(lineTab1 != null) lineTab1.Color = Color.FromArgb("#B71C1C");
                if(tab1Content != null) tab1Content.IsVisible = true;
            }
            else if (selectedTabIndex == 2)
            {
                if(lblTab2 != null) lblTab2.TextColor = Color.FromArgb("#D4AF37");
                if(lineTab2 != null) lineTab2.Color = Color.FromArgb("#B71C1C");
                if(tab2Content != null) tab2Content.IsVisible = true;
            }
            else if (selectedTabIndex == 3)
            {
                if(lblTab3 != null) lblTab3.TextColor = Color.FromArgb("#D4AF37");
                if(lineTab3 != null) lineTab3.Color = Color.FromArgb("#B71C1C");
                if(tab3Content != null) tab3Content.IsVisible = true;
            }
        }

        private async Task HandleLanguageSelection(string languageCode, string displayName)
        {
            try
            {
                _ttsService.SetLanguage(languageCode);

                // Cập nhật nút ngôn ngữ
                var languageButton = this.FindByName<Button>("LanguageButton");
                if (languageButton != null)
                {
                    string buttonText = !string.IsNullOrEmpty(displayName) ? displayName : languageCode;
                    languageButton.Text = $"{buttonText} ▾";
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", $"Không thể cập nhật ngôn ngữ: {ex.Message}", "OK");
            }
        }

        private async void OnAddLanguageClicked(object sender, EventArgs e)
        {
            try
            {
                // Dừng TTS đang phát
                if (_ttsService.IsPlaying)
                {
                    _ttsService.StopSpeaking();
                    var playPauseIcon = this.FindByName<Label>("PlayPauseIcon");
                    if (playPauseIcon != null)
                    {
                        playPauseIcon.Text = PlayIconGlyph;
                    }
                }

                // Mở trang tìm kiếm ngôn ngữ
                await Shell.Current.GoToAsync("languagesearch");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", $"Không thể mở trang chọn ngôn ngữ: {ex.Message}", "OK");
            }
        }

        private async void OnSettingsClicked(object sender, EventArgs e)
        {
            try
            {
                if (_ttsService.AvailableLocales == null)
                    return;

                if (string.IsNullOrEmpty(_ttsService.SelectedLanguageCode))
                {
                    await DisplayAlert("Thông báo", "Không tìm thấy giọng nói nào trên thiết bị.", "OK");
                    return;
                }

                var voicesForLanguage = _ttsService.GetVoicesForLanguage(_ttsService.SelectedLanguageCode);

                if (!voicesForLanguage.Any())
                {
                    await DisplayAlert("Thông báo", "Không tìm thấy giọng đọc nào cho ngôn ngữ này.", "OK");
                    return;
                }

                var voiceNames = voicesForLanguage.Select(x => x.Name).ToArray();
                string action = await DisplayActionSheet($"Chọn giọng ({_ttsService.SelectedLanguageCode})", "Đóng", null, voiceNames);

                if (action != null && action != "Đóng")
                {
                    var selectedVoice = voicesForLanguage.FirstOrDefault(x => x.Name == action);
                    if (selectedVoice != null)
                    {
                        _ttsService.SetVoice(selectedVoice);
                        await DisplayAlert("Thành công", $"Đã chọn giọng: {selectedVoice.Name}", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", $"Không thể tải danh sách giọng nói: {ex.Message}", "OK");
            }
        }

        private async void OnPlayPauseTapped(object sender, EventArgs e)
        {
            var ttsSwitch = this.FindByName<Switch>("TtsSwitch");
            if (ttsSwitch != null && !ttsSwitch.IsToggled)
                return;

            if (_ttsService.IsPlaying)
            {
                _ttsService.StopSpeaking();
            }
            else
            {
                var textToSpeak = !string.IsNullOrWhiteSpace(_currentDescriptionVietnamese)
                    ? _currentDescriptionVietnamese
                    : this.FindByName<Label>("DescriptionLabel")?.Text;

                if (!string.IsNullOrWhiteSpace(textToSpeak))
                {
                    await _ttsService.SpeakAsync(textToSpeak);
                }
            }
        }

        private void UpdateTab1Content(string source, string name, string description)
        {
            // Lưu mô tả gốc bằng tiếng Việt
            _currentDescriptionVietnamese = description;

            // Dừng TTS nếu đang phát
            if (_ttsService.IsPlaying)
            {
                _ttsService.StopSpeaking();
            }

            var tab1Content = this.FindByName<Border>("Tab1Content");
            if (tab1Content == null)
                return;

            var statusHStack = tab1Content.Content as VerticalStackLayout;
            if (statusHStack != null && statusHStack.Children.Count > 0)
            {
                // Cập nhật nhãn trạng thái
                if (statusHStack.Children[0] is HorizontalStackLayout statusLayout && statusLayout.Children.Count > 1)
                {
                    if (statusLayout.Children[1] is Label statusLabel)
                    {
                        statusLabel.Text = $"{source} · {name}";
                    }
                }

                // Cập nhật tiêu đề
                if (statusHStack.Children.Count > 1 && statusHStack.Children[1] is Label titleLabel)
                {
                    titleLabel.Text = name;
                }

                // Cập nhật mô tả
                var descriptionLabel = this.FindByName<Label>("DescriptionLabel");
                if (descriptionLabel != null)
                {
                    descriptionLabel.Text = description;
                }
                else if (statusHStack.Children.Count > 2 && statusHStack.Children[2] is ScrollView sv && sv.Content is Label svDescLabel)
                {
                    svDescLabel.Text = description;
                }
                else if (statusHStack.Children.Count > 2 && statusHStack.Children[2] is Label oldDescLabel)
                {
                    oldDescLabel.Text = description;
                }
            }
        }

        private void ExpandDrawerIfNeeded()
        {
            if (!isExpanded)
            {
                var expandableContent = this.FindByName<VerticalStackLayout>("ExpandableContent");
                if (expandableContent != null)
                {
                    isExpanded = true;
                    expandableContent.Opacity = 0;

                    Animation parentAnimation = new Animation();
                    var heightAnimation = new Animation(v => expandableContent.HeightRequest = v, 0, 400, Easing.CubicOut);
                    var fadeAnimation = new Animation(v => expandableContent.Opacity = v, 0, 1, Easing.CubicIn);

                    parentAnimation.Add(0, 1, heightAnimation);
                    parentAnimation.Add(0.5, 1, fadeAnimation);
                    parentAnimation.Commit(this, "ExpandDrawer", 16, 300);
                }
            }
        }

        private async void OnPlaceSelected(string name, string description, string source)
        {
            UpdateTab1Content(source, name, description);
            UpdateTabVisuals(1);
            ExpandDrawerIfNeeded();

            // Tự động phát TTS nếu được bật
            var ttsSwitch = this.FindByName<Switch>("TtsSwitch");
            if (ttsSwitch != null && ttsSwitch.IsToggled && !_ttsService.IsPlaying)
            {
                await Task.Delay(100);
                await _ttsService.SpeakAsync(description);
            }
        }
    }
}
