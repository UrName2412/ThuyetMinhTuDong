using ThuyetMinhTuDong.Models;
using ThuyetMinhTuDong.ViewModels;
using Microsoft.Maui.Controls.Compatibility;

namespace ThuyetMinhTuDong
{
    [QueryProperty(nameof(SelectedLanguageCode), "selectedLanguageCode")]
    [QueryProperty(nameof(SelectedLanguageDisplay), "selectedLanguageDisplay")]
    [QueryProperty(nameof(QrPoiId), "qrPoiId")]
    public partial class MainPage : ContentPage
    {
        private const string PlayIconGlyph = "\uf04b";
        private const string PauseIconGlyph = "\uf04c";
        private const double ApproachRadiusMeters = 80;

        private readonly MainPageViewModel _viewModel;

        private string _selectedLanguageCodeParam;
        private string _selectedLanguageDisplayParam;
        private string _qrPoiIdParam;
        private bool isExpanded = false;
        private bool _isGpsRealtimeEnabled;
        private CancellationTokenSource? _gpsTrackingCts;
        private int? _lastAutoSpokenPoiId;
        private int? _lastApproachPoiId;
        private Location? _lastLoadedLocation;

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

        public string QrPoiId
        {
            get => _qrPoiIdParam;
            set
            {
                _qrPoiIdParam = value;
                TryHandleQrPoiSelection();
            }
        }

        // Xử lý khi có ID POI quét từ QR code truyền về.
        private void TryHandleQrPoiSelection()
        {
            if (!string.IsNullOrEmpty(_qrPoiIdParam))
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    if (int.TryParse(_qrPoiIdParam, out int poiId))
                    {
                        var pois = await _viewModel.GetAllActivePoisFromCacheAsync();
                        var poi = pois?.FirstOrDefault(p => p.Id == poiId);
                        
                        if (poi != null)
                        {
                            OnPlaceSelected(poi, "Quét QR");
                        }
                        else
                        {
                            await DisplayAlert("Mã QR", "Không tìm thấy thông tin địa điểm này.", "OK");
                        }
                    }
                    _qrPoiIdParam = null;
                });
            }
        }

        // Nhận tham số ngôn ngữ từ query và áp dụng khi đủ dữ liệu.
        private void TryHandleLanguageSelection()
        {
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

        // Khởi tạo trang chính và đăng ký các event UI/ViewModel.
        public MainPage(MainPageViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
            SubscribeToViewModelEvents();
            WireImagePreviewEvents();
        }

        // Gắn sự kiện cho các thành phần giao diện như GPS và preview ảnh.
        private void WireImagePreviewEvents()
        {
            var closeButton = this.FindByName<Button>("CloseImagePreviewButton");
            if (closeButton != null)
            {
                closeButton.Clicked += OnCloseImagePreviewClicked;
            }

            var backdrop = this.FindByName<BoxView>("ImagePreviewBackdrop");
            if (backdrop != null)
            {
                var tap = new TapGestureRecognizer();
                tap.Tapped += OnImagePreviewBackgroundTapped;
                backdrop.GestureRecognizers.Add(tap);
            }

            var gpsButton = this.FindByName<Button>("GpsToggleButton");
            if (gpsButton != null)
            {
                gpsButton.Clicked += OnGpsToggleClicked;
            }

            var qrButton = this.FindByName<Button>("QrScanButton");
            if (qrButton != null)
            {
                qrButton.Clicked += OnQrScanClicked;
            }
        }

        // Lắng nghe event từ ViewModel để cập nhật trạng thái UI.
        private void SubscribeToViewModelEvents()
        {
            _viewModel.PlayStarted += (s, e) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var playPauseIcon = this.FindByName<Label>("PlayPauseIcon");
                    if (playPauseIcon != null) playPauseIcon.Text = PauseIconGlyph;
                });
            };

            _viewModel.PlayStopped += (s, e) =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var playPauseIcon = this.FindByName<Label>("PlayPauseIcon");
                    if (playPauseIcon != null) playPauseIcon.Text = PlayIconGlyph;
                });
            };

            _viewModel.PermissionDenied += async (s, message) =>
            {
                await DisplayAlert("Quyền bị từ chối", message, "OK");
            };

            _viewModel.TranslationStarted += async (s, targetLangName) =>
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    try
                    {
                        string msg = string.IsNullOrWhiteSpace(targetLangName) 
                            ? "Đang dịch sang ngôn ngữ của bạn..."
                            : $"Đang dịch sang {targetLangName}...";

                        var toast = CommunityToolkit.Maui.Alerts.Toast.Make(msg, CommunityToolkit.Maui.Core.ToastDuration.Short, 14);
                        await toast.Show();
                    }
                    catch { }
                });
            };
        }

        // Xử lý khi trang xuất hiện: khởi tạo dữ liệu và xin quyền vị trí.
        protected override void OnAppearing()
        {
            base.OnAppearing();

            UpdateGpsToggleButton();

            Task.Run(async () =>
            {
                try
                {
                    await _viewModel.InitializeAsync();

                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        try
                        {
                            var languageButton = this.FindByName<Button>("LanguageButton");
                            if (languageButton != null)
                            {
                                languageButton.Text = _viewModel.LanguageButtonText;
                            }

                            await CheckAndRequestLocationPermission();

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
                catch
                {
                }
            });
        }

        // Xử lý khi trang ẩn: dừng GPSRealtime và tắt hiển thị vị trí người dùng.
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            StopRealtimeGpsTracking();

            if (this.FindByName<Microsoft.Maui.Controls.Maps.Map>("MyMap") is { } map)
            {
                map.IsShowingUser = false;
            }
        }

        // Kiểm tra quyền vị trí, lấy vị trí hiện tại và nạp POI lên bản đồ.
        private async Task CheckAndRequestLocationPermission()
        {
            try
            {
                bool permissionGranted = await _viewModel.CheckAndRequestLocationPermissionAsync();

                if (permissionGranted)
                {
                    var location = await _viewModel.GetCurrentLocationAsync();

                    if (location != null)
                    {
                        UpdateCoordinateCard(location);

                        if (this.FindByName<Microsoft.Maui.Controls.Maps.Map>("MyMap") is { } map)
                        {
                            map.IsShowingUser = true;

                            var mapSpan = _viewModel.CreateMapSpan(location);
                            map.MoveToRegion(mapSpan);

                            _lastLoadedLocation = location;
                            await AddPOIsToMapAsync(map, location, true);
                        }

                        if (!_isGpsRealtimeEnabled)
                        {
                            StartRealtimeGpsTracking();
                        }
                    }
                }
            }
            catch
            {
            }
        }

        // Bật hoặc tắt cơ chế theo dõi GPS realtime khi người dùng bấm nút.
        private async void OnGpsToggleClicked(object sender, EventArgs e)
        {
            if (_isGpsRealtimeEnabled)
            {
                StopRealtimeGpsTracking();
                return;
            }

            var permissionGranted = await _viewModel.CheckAndRequestLocationPermissionAsync();
            if (!permissionGranted)
            {
                await DisplayAlert("Quyền vị trí", "Cần bật quyền vị trí để theo dõi.", "OK");
                return;
            }

            StartRealtimeGpsTracking();
        }

        // Khởi động vòng lặp nền đọc vị trí định kỳ để cập nhật map và auto TTS.
        private void StartRealtimeGpsTracking()
        {
            StopRealtimeGpsTracking();

            _isGpsRealtimeEnabled = true;
            UpdateGpsToggleButton();
            _gpsTrackingCts = new CancellationTokenSource();
            var token = _gpsTrackingCts.Token;

            _ = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var location = await _viewModel.GetCurrentLocationAsync();
                        if (location != null)
                        {
                            bool shouldReloadPOIs = _lastLoadedLocation == null || 
                                Location.CalculateDistance(
                                    location.Latitude, location.Longitude, 
                                    _lastLoadedLocation.Latitude, _lastLoadedLocation.Longitude, 
                                    Microsoft.Maui.Devices.Sensors.DistanceUnits.Kilometers) * 1000 > 500;

                            if (shouldReloadPOIs)
                            {
                                _lastLoadedLocation = location;

                                // Bắn lệnh cập nhật Map/Danh sách qua UI Thread mốc thời gian nhưng không dùng await để khóa tiến trình loop.
                                MainThread.BeginInvokeOnMainThread(async () =>
                                {
                                    if (this.FindByName<Microsoft.Maui.Controls.Maps.Map>("MyMap") is { } map)
                                    {
                                        await AddPOIsToMapAsync(map, location, false);
                                    }
                                });
                            }

                            // Chạy UI Update tọa độ nhẹ nhàng
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                UpdateCoordinateCard(location);
                            });

                            // Ưu tiên Thuyết minh không bị trễ theo quá trình update Map
                            await HandleAutoPoiAnnouncementAsync(location);
                        }
                    }
                    catch
                    {
                    }

                    try
                    {
                        await Task.Delay(2000, token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }
            }, token);
        }

        // Dừng vòng lặp GPSRealtime và reset trạng thái liên quan.
        private void StopRealtimeGpsTracking()
        {
            _gpsTrackingCts?.Cancel();
            _gpsTrackingCts?.Dispose();
            _gpsTrackingCts = null;

            _isGpsRealtimeEnabled = false;
            _lastAutoSpokenPoiId = null;
            _lastApproachPoiId = null;
            UpdateGpsToggleButton();
        }

        // Cập nhật trạng thái hiển thị của nút GPS ON/OFF.
        private void UpdateGpsToggleButton()
        {
            var gpsToggleButton = this.FindByName<Button>("GpsToggleButton");
            if (gpsToggleButton != null)
            {
                gpsToggleButton.Text = _isGpsRealtimeEnabled ? "GPS: ON" : "GPS: OFF";
            }
        }

        // Cập nhật thẻ tọa độ hiện tại của người dùng trên UI.
        private void UpdateCoordinateCard(Location location)
        {
            var coordinatesLabel = this.FindByName<Label>("UserCoordinatesLabel");
            if (coordinatesLabel != null)
            {
                coordinatesLabel.Text = $"📍 Lat: {location.Latitude:F6}\nLng: {location.Longitude:F6}";
            }
        }

        // Nạp POI gần vị trí hiện tại và render pin/vòng trigger lên bản đồ.
        private async Task AddPOIsToMapAsync(Microsoft.Maui.Controls.Maps.Map map, Location userLocation, bool syncApi = true)
        {
            map.Pins.Clear();
            map.MapElements.Clear(); // Xóa các đường tròn cũ

            var nearbyList = this.FindByName<VerticalStackLayout>("NearbyPlacesList");
            var emptyLabel = this.FindByName<Label>("EmptyNearbyLabel");

            if (nearbyList != null)
            {
                nearbyList.Children.Clear();
            }

            if (emptyLabel != null)
            {
                emptyLabel.Text = "Đang tải địa điểm...";
                emptyLabel.IsVisible = true;
            }

            try
            {
                await _viewModel.LoadNearbyPoisAsync(userLocation, syncApi);

                if (emptyLabel != null)
                {
                    emptyLabel.Text = _viewModel.NearbyStatusText;
                    emptyLabel.IsVisible = _viewModel.IsNearbyStatusVisible;
                }

                foreach (var poi in _viewModel.NearbyPois)
                {
                    var pin = _viewModel.CreateMapPin(poi);
                    if (pin != null)
                    {
                        pin.MarkerClicked += (s, args) =>
                        {
                            OnPlaceSelected(poi, "Bản đồ");
                        };

                        map.Pins.Add(pin);
                    }

                    // Thêm hình tròn hiển thị bán kính Trigger cho mỗi POI
                    double triggerRadiusMeters = Math.Max(1, poi.Radius ?? 30);
                    var triggerCircle = new Microsoft.Maui.Controls.Maps.Circle
                    {
                        Center = new Location(poi.Latitude, poi.Longitude),
                        Radius = new Microsoft.Maui.Maps.Distance(triggerRadiusMeters),
                        StrokeColor = Colors.Red,
                        StrokeWidth = 2,
                        FillColor = Color.FromRgba(255, 0, 0, 30) // Đỏ trong suốt
                    };
                    map.MapElements.Add(triggerCircle);

                    if (nearbyList != null)
                    {
                        await CreateAndAddNearbyPlaceItemAsync(nearbyList, poi);
                    }
                }
            }
            catch
            {
                if (emptyLabel != null)
                {
                    emptyLabel.Text = "Không thể tải danh sách địa điểm.";
                    emptyLabel.IsVisible = true;
                }
            }
        }

        // Tạo item giao diện cho một POI và thêm vào danh sách gần bạn.
        private async Task CreateAndAddNearbyPlaceItemAsync(VerticalStackLayout nearbyList, PointOfInterest poi)
        {
            var itemLayout = new Border
            {
                BackgroundColor = Color.FromArgb("#1A1111"),
                Stroke = Color.FromArgb("#B71C1C"),
                Padding = 10,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 }
            };

            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += (s, e) =>
            {
                OnPlaceSelected(poi, "Gần bạn");
            };
            itemLayout.GestureRecognizers.Add(tapGesture);

            var imageUrl = await _viewModel.GetFirstImageUrlForPoiAsync(poi.Id, poi.Name);

            var cardGrid = new Microsoft.Maui.Controls.Grid
            {
                ColumnDefinitions = new Microsoft.Maui.Controls.ColumnDefinitionCollection
                {
                    new Microsoft.Maui.Controls.ColumnDefinition { Width = new GridLength(90) },
                    new Microsoft.Maui.Controls.ColumnDefinition { Width = GridLength.Star }
                },
                ColumnSpacing = 12,
                VerticalOptions = LayoutOptions.Center
            };

            var imageBorder = new Border
            {
                Stroke = Color.FromArgb("#3A2121"),
                StrokeThickness = 1,
                Padding = 0,
                HeightRequest = 70,
                WidthRequest = 90,
                BackgroundColor = Color.FromArgb("#111111"),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 }
            };

            imageBorder.Content = new Microsoft.Maui.Controls.Image
            {
                Source = !string.IsNullOrWhiteSpace(imageUrl)
                    ? ImageSource.FromUri(new Uri(imageUrl))
                    : "dotnet_bot.png",
                Aspect = Aspect.AspectFill,
                HeightRequest = 70,
                WidthRequest = 90
            };

            var nameLabel = new Label
            {
                Text = poi.Name,
                TextColor = Colors.White,
                FontAttributes = FontAttributes.Bold,
                FontSize = 15,
                VerticalOptions = LayoutOptions.Center,
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 2
            };

            cardGrid.Add(imageBorder);
            Microsoft.Maui.Controls.Grid.SetColumn(imageBorder, 0);

            cardGrid.Add(nameLabel);
            Microsoft.Maui.Controls.Grid.SetColumn(nameLabel, 1);

            itemLayout.Content = cardGrid;
            nearbyList.Children.Add(itemLayout);
        }

        // Mở rộng panel thông tin bằng thao tác vuốt lên.
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

                var heightAnimation = new Animation(v => expandableContent.HeightRequest = v, 0, 480, Easing.CubicOut);
                var fadeAnimation = new Animation(v => expandableContent.Opacity = v, 0, 1, Easing.CubicIn);

                parentAnimation.Add(0, 1, heightAnimation);
                parentAnimation.Add(0.5, 1, fadeAnimation);

                parentAnimation.Commit(this, "ExpandDrawer", 16, 300);

                if (coordBorder != null)
                {
                    _ = coordBorder.TranslateTo(0, -320, 300, Easing.CubicOut);
                }
            }
        }

        // Thu gọn panel thông tin bằng thao tác vuốt xuống.
        private async void OnSwipeDown(object sender, SwipedEventArgs e)
        {
            if (!isExpanded) return;
            isExpanded = false;

            var expandableContent = this.FindByName<VerticalStackLayout>("ExpandableContent");
            var coordBorder = this.FindByName<Border>("CoordBorder");

            if (expandableContent != null)
            {
                Animation parentAnimation = new Animation();

                var heightAnimation = new Animation(v => expandableContent.HeightRequest = v, 480, 0, Easing.CubicIn);
                var fadeAnimation = new Animation(v => expandableContent.Opacity = v, 1, 0, Easing.CubicOut);

                parentAnimation.Add(0, 1, heightAnimation);
                parentAnimation.Add(0, 0.5, fadeAnimation);

                parentAnimation.Commit(this, "CollapseDrawer", 16, 300);

                if (coordBorder != null)
                {
                    _ = coordBorder.TranslateTo(0, 0, 300, Easing.CubicIn);
                }
            }
        }

        // Chọn tab 1 trong phần nội dung.
        private void OnTab1Tapped(object sender, EventArgs e)
        {
            UpdateTabVisuals(1);
        }

        // Chọn tab 2 trong phần nội dung.
        private void OnTab2Tapped(object sender, EventArgs e)
        {
            UpdateTabVisuals(2);
        }

        // Cập nhật trạng thái màu sắc và nội dung theo tab đang chọn.
        private void UpdateTabVisuals(int selectedTabIndex)
        {
            var lblTab1 = this.FindByName<Label>("LblTab1");
            var lineTab1 = this.FindByName<BoxView>("LineTab1");
            var lblTab2 = this.FindByName<Label>("LblTab2");
            var lineTab2 = this.FindByName<BoxView>("LineTab2");

            var tab1Content = this.FindByName<View>("Tab1Content");
            var tab2Content = this.FindByName<View>("Tab2Content");

            if(lblTab1 != null) lblTab1.TextColor = Color.FromArgb("#A0938A");
            if(lineTab1 != null) lineTab1.Color = Color.FromArgb("#3A2121");
            if(lblTab2 != null) lblTab2.TextColor = Color.FromArgb("#A0938A");
            if(lineTab2 != null) lineTab2.Color = Color.FromArgb("#3A2121");

            if(tab1Content != null) tab1Content.IsVisible = false;
            if(tab2Content != null) tab2Content.IsVisible = false;

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
        }

        // Áp dụng ngôn ngữ được chọn và cập nhật text hiển thị của nút ngôn ngữ.
        private async Task HandleLanguageSelection(string languageCode, string displayName)
        {
            try
            {
                await _viewModel.HandleLanguageSelectionAsync(languageCode, displayName);

                var languageButton = this.FindByName<Button>("LanguageButton");
                if (languageButton != null)
                {
                    languageButton.Text = _viewModel.LanguageButtonText;
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", $"Không thể cập nhật ngôn ngữ: {ex.Message}", "OK");
            }
        }

        // Mở màn hình quét QR.
        private async void OnQrScanClicked(object sender, EventArgs e)
        {
            try
            {
                await Shell.Current.GoToAsync("qrscanner");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", $"Không thể mở màn quét QR: {ex.Message}", "OK");
            }
        }

        // Mở màn hình chọn ngôn ngữ và dừng TTS nếu đang phát.
        private async void OnAddLanguageClicked(object sender, EventArgs e)
        {
            try
            {
                // Dừng TTS đang phát
                if (_viewModel.IsPlaying)
                {
                    _viewModel.StopSpeaking();
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

        // Mở danh sách giọng đọc theo ngôn ngữ hiện tại để người dùng chọn.
        private async void OnSettingsClicked(object sender, EventArgs e)
        {
            try
            {
                if (_viewModel.AvailableLocales == null)
                    return;

                if (string.IsNullOrEmpty(_viewModel.SelectedLanguageCode))
                {
                    await DisplayAlert("Thông báo", "Không tìm thấy giọng nói nào trên thiết bị.", "OK");
                    return;
                }

                var voicesForLanguage = _viewModel.GetVoicesForLanguage(_viewModel.SelectedLanguageCode);

                if (!voicesForLanguage.Any())
                {
                    await DisplayAlert("Thông báo", "Không tìm thấy giọng đọc nào cho ngôn ngữ này.", "OK");
                    return;
                }

                var voiceNames = voicesForLanguage.Select(x => x.Name).ToArray();
                string action = await DisplayActionSheet($"Chọn giọng ({_viewModel.SelectedLanguageCode})", "Đóng", null, voiceNames);

                if (action != null && action != "Đóng")
                {
                    var selectedVoice = voicesForLanguage.FirstOrDefault(x => x.Name == action);
                    if (selectedVoice != null)
                    {
                        _viewModel.SetVoice(selectedVoice);
                        await DisplayAlert("Thành công", $"Đã chọn giọng: {selectedVoice.Name}", "OK");
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", $"Không thể tải danh sách giọng nói: {ex.Message}", "OK");
            }
        }

        // Xử lý nút Play/Pause để phát hoặc dừng thuyết minh thủ công.
        private async void OnPlayPauseTapped(object sender, EventArgs e)
        {
            var ttsSwitch = this.FindByName<Switch>("TtsSwitch");
            if (ttsSwitch != null && !ttsSwitch.IsToggled)
                return;

            if (_viewModel.IsPlaying)
            {
                _viewModel.StopSpeaking();
            }
            else
            {
                var textToSpeak = this.FindByName<Label>("DescriptionLabel")?.Text;

                if (!string.IsNullOrWhiteSpace(textToSpeak))
                {
                    await _viewModel.SpeakAsync(textToSpeak);
                }
            }
        }

        // Cập nhật dữ liệu hiển thị chi tiết POI ở tab thông tin.
        private void UpdateTab1Content(string source, string name, string description, string mapLink)
        {
            _viewModel.CurrentDescriptionVietnamese = description;
            _viewModel.CurrentMapLink = mapLink;

            // Dừng TTS nếu đang phát
            if (_viewModel.IsPlaying)
            {
                _viewModel.StopSpeaking();
            }

            var openMapBtn = this.FindByName<Button>("OpenMapButton");
            if (openMapBtn != null)
            {
                openMapBtn.IsVisible = !string.IsNullOrWhiteSpace(mapLink);
            }

            var tab1Content = this.FindByName<Border>("Tab1Content");
            if (tab1Content == null)
                return;

            VerticalStackLayout statusHStack = null;
            if (tab1Content.Content is ScrollView sv && sv.Content is VerticalStackLayout svVsl)
            {
                statusHStack = svVsl;
                
                // Tiện cập nhật nút ở đây vì nếu nó nằm trong cùng cây View
                var btnInScroll = svVsl.FindByName<Button>("OpenMapButton");
                if (btnInScroll != null)
                {
                    btnInScroll.IsVisible = !string.IsNullOrWhiteSpace(mapLink);
                }
            }
            else if (tab1Content.Content is VerticalStackLayout vsl)
            {
                statusHStack = vsl;
            }

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
                else if (statusHStack.Children.Count > 2 && statusHStack.Children[2] is ScrollView svDesc && svDesc.Content is Label svDescLabel)
                {
                    svDescLabel.Text = description;
                }
                else if (statusHStack.Children.Count > 2 && statusHStack.Children[2] is Label oldDescLabel)
                {
                    oldDescLabel.Text = description;
                }
            }
        }

        // Mở rộng drawer nếu hiện tại đang thu gọn.
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
                    var heightAnimation = new Animation(v => expandableContent.HeightRequest = v, 0, 480, Easing.CubicOut);
                    var fadeAnimation = new Animation(v => expandableContent.Opacity = v, 0, 1, Easing.CubicIn);

                    parentAnimation.Add(0, 1, heightAnimation);
                    parentAnimation.Add(0.5, 1, fadeAnimation);
                    parentAnimation.Commit(this, "ExpandDrawer", 16, 300);

                    var coordBorder = this.FindByName<Border>("CoordBorder");
                    if (coordBorder != null)
                    {
                        _ = coordBorder.TranslateTo(0, -320, 300, Easing.CubicOut);
                    }
                }
            }
        }

        // Xử lý khi người dùng chọn một POI từ map hoặc danh sách.
        private async void OnPlaceSelected(PointOfInterest poi, string source)
        {
            System.Diagnostics.Debug.WriteLine($"[UI] Selected POI: {poi.Name}, MapLink: '{poi.MapLink}'");
            UpdateTab1Content(source, poi.Name, poi.Description, poi.MapLink);
            UpdateTabVisuals(1);
            ExpandDrawerIfNeeded();

            // Lấy danh sách ảnh cho POI vừa chọn
            await _viewModel.LoadImagesForPoiAsync(poi.Id, poi.Name);

            // Tự động phát TTS nếu được bật
            var ttsSwitch = this.FindByName<Switch>("TtsSwitch");
            if (ttsSwitch != null && ttsSwitch.IsToggled && !_viewModel.IsPlaying)
            {
                await Task.Delay(100);
                await _viewModel.SpeakAsync(poi.Description);
            }
        }

        // Hiển thị overlay preview khi người dùng chạm vào ảnh POI.
        private void OnPoiImageTapped(object sender, TappedEventArgs e)
        {
            if (sender is not BindableObject bindable || bindable.BindingContext is not ThuyetMinhTuDong.Models.Image tappedImage)
                return;

            if (_viewModel.PoiImages == null || _viewModel.PoiImages.Count == 0)
                return;

            var index = _viewModel.PoiImages
                .Select((img, idx) => new { img, idx })
                .FirstOrDefault(x => x.img.Id == tappedImage.Id && x.img.ImageUrl == tappedImage.ImageUrl)?.idx ?? 0;

            var carousel = this.FindByName<CarouselView>("PreviewCarousel");
            if (carousel != null)
            {
                carousel.Position = index;
            }

            var overlay = this.FindByName<Microsoft.Maui.Controls.Grid>("ImagePreviewOverlay");
            if (overlay != null)
            {
                overlay.IsVisible = true;
            }
        }

        // Đóng preview ảnh khi bấm nút đóng.
        private void OnCloseImagePreviewClicked(object sender, EventArgs e)
        {
            CloseImagePreview();
        }

        // Đóng preview ảnh khi chạm vùng nền tối.
        private void OnImagePreviewBackgroundTapped(object sender, TappedEventArgs e)
        {
            CloseImagePreview();
        }

        // Mở liên kết bản đồ của POI bằng ứng dụng bản đồ mặc định.
        private async void OnOpenMapClicked(object sender, EventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_viewModel.CurrentMapLink))
                {
                    await Launcher.Default.OpenAsync(new Uri(_viewModel.CurrentMapLink));
                }
            }   catch (Exception ex)
            {
                await DisplayAlert("Lỗi", $"Không thể mở bản đồ: {ex.Message}", "OK");
            }
        }

        // Tự động xác định POI gần nhất để cập nhật UI và phát TTS theo vùng trigger.
        private async Task HandleAutoPoiAnnouncementAsync(Location currentLocation)
        {
            bool isTtsToggled = false;
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var ttsSwitch = this.FindByName<Switch>("TtsSwitch");
                isTtsToggled = ttsSwitch != null && ttsSwitch.IsToggled;
            });

            if (!isTtsToggled)
                return;

            if (_viewModel.IsPlaying)
                return;

            var activePois = await _viewModel.GetAllActivePoisFromCacheAsync();
            if (activePois == null || activePois.Count == 0)
            {
                _lastAutoSpokenPoiId = null;
                _lastApproachPoiId = null;
                return;
            }

            PointOfInterest nearestPoiModel = null;
            double nearestPoiDistance = double.MaxValue;
            double nearestPoiTriggerRadius = 30;

            foreach (var poi in activePois)
            {
                double dist = Location.CalculateDistance(
                    currentLocation.Latitude,
                    currentLocation.Longitude,
                    poi.Latitude,
                    poi.Longitude,
                    Microsoft.Maui.Devices.Sensors.DistanceUnits.Kilometers) * 1000d;

                if (dist <= ApproachRadiusMeters && dist < nearestPoiDistance)
                {
                    nearestPoiDistance = dist;
                    nearestPoiModel = poi;
                    nearestPoiTriggerRadius = Math.Max(1, poi.Radius ?? 30);
                }
            }

            string debugNewText = "";

            if (nearestPoiModel == null)
            {
                debugNewText = "Không có điểm nào < 80m";
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var debugLabel = this.FindByName<Label>("DebugTriggerLabel");
                    if (debugLabel != null) debugLabel.Text = debugNewText;
                });

                _lastAutoSpokenPoiId = null;
                _lastApproachPoiId = null;
                return;
            }

            debugNewText = $"Gần: {nearestPoiModel.Name} ({nearestPoiDistance:N0}m / {nearestPoiTriggerRadius}m)";

            if (nearestPoiDistance > nearestPoiTriggerRadius)
            {
                debugNewText += " - Đã tiếp cận (Chưa phát TTS)";

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var debugLabel = this.FindByName<Label>("DebugTriggerLabel");
                    if (debugLabel != null) debugLabel.Text = debugNewText;
                });

                // Đặt lại cờ để khi bước vào vùng Trigger sẽ phát lại TTS
                if (_lastAutoSpokenPoiId == nearestPoiModel.Id)
                {
                    _lastAutoSpokenPoiId = null;
                }

                if (_lastApproachPoiId != nearestPoiModel.Id)
                {
                    _lastApproachPoiId = nearestPoiModel.Id;
                    await _viewModel.LoadImagesForPoiAsync(nearestPoiModel.Id, nearestPoiModel.Name);
                }
                return;
            }

            debugNewText += " - Đã phát TTS !!!";

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var debugLabel = this.FindByName<Label>("DebugTriggerLabel");
                if (debugLabel != null) debugLabel.Text = debugNewText;
            });

            if (_lastAutoSpokenPoiId == nearestPoiModel.Id)
                return;

            _lastApproachPoiId = nearestPoiModel.Id;
            _lastAutoSpokenPoiId = nearestPoiModel.Id;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                UpdateTab1Content("Tự động gần bạn", nearestPoiModel.Name, nearestPoiModel.Description, nearestPoiModel.MapLink);
                UpdateTabVisuals(1);
                ExpandDrawerIfNeeded();
            });

            await _viewModel.LoadImagesForPoiAsync(nearestPoiModel.Id, nearestPoiModel.Name);
            await _viewModel.AutoSpeakAsync(true, nearestPoiModel.Description);
        }

        // Ẩn overlay preview ảnh.
        private void CloseImagePreview()
        {
            var overlay = this.FindByName<Microsoft.Maui.Controls.Grid>("ImagePreviewOverlay");
            if (overlay != null)
            {
                overlay.IsVisible = false;
            }
        }
    }
}
