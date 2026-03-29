using ThuyetMinhTuDong.Data;
using ThuyetMinhTuDong.Models;
using ThuyetMinhTuDong.Services;

namespace ThuyetMinhTuDong
{
    [QueryProperty(nameof(QrId), "qrId")]
    [QueryProperty(nameof(SelectedLanguageCode), "selectedLanguageCode")]
    [QueryProperty(nameof(SelectedLanguageDisplay), "selectedLanguageDisplay")]
    public partial class MainPage : ContentPage
    {
        private readonly LocalDatabase _database;
        private string _qrIdParam;
        private string _selectedLanguageCodeParam;
        private string _selectedLanguageDisplayParam;

        public string QrId
        {
            get => _qrIdParam;
            set
            {
                _qrIdParam = value;
                // Handle the QR code when query parameter is set
                if (!string.IsNullOrEmpty(_qrIdParam))
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await HandleQRCodeResult(_qrIdParam);
                        _qrIdParam = null;
                    });
                }
            }
        }

        public string SelectedLanguageCode
        {
            get => _selectedLanguageCodeParam;
            set
            {
                _selectedLanguageCodeParam = value;
                if (!string.IsNullOrEmpty(_selectedLanguageCodeParam))
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await HandleLanguageSelection(_selectedLanguageCodeParam, _selectedLanguageDisplayParam);
                        _selectedLanguageCodeParam = null;
                    });
                }
            }
        }

        public string SelectedLanguageDisplay
        {
            get => _selectedLanguageDisplayParam;
            set => _selectedLanguageDisplayParam = value;
        }

        public MainPage(LocalDatabase database)
        {
            InitializeComponent();
            _database = database;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Initialize sample QR codes on first run
            await QRCodeService.InitializeSampleQRCodesAsync(_database);

            await CheckAndRequestLocationPermission();

            // Tối ưu: Bật định vị khi trang xuất hiện
            if (this.FindByName<Microsoft.Maui.Controls.Maps.Map>("MyMap") is { } map)
            {
                map.IsShowingUser = true;
            }

            try
            {
                if (_availableLocales == null)
                {
                    _availableLocales = await TextToSpeech.Default.GetLocalesAsync();
                }

                if (string.IsNullOrEmpty(_selectedLanguageCode) && _availableLocales != null && _availableLocales.Any())
                {
                    // Ưu tiên chọn Tiếng Việt làm mặc định, nếu không có lấy ngôn ngữ đầu tiên
                    _selectedLocale = _availableLocales.FirstOrDefault(x => x.Language.StartsWith("vi", StringComparison.OrdinalIgnoreCase)) 
                                      ?? _availableLocales.FirstOrDefault();
                    _selectedLanguageCode = _selectedLocale?.Language;

                    if (!string.IsNullOrEmpty(_selectedLanguageCode))
                    {
                        var languageButton = this.FindByName<Button>("LanguageButton");
                        if (languageButton != null)
                        {
                            string displayName = _selectedLanguageCode;
                            try
                            {
                                var culture = new System.Globalization.CultureInfo(_selectedLanguageCode);
                                displayName = culture.NativeName;
                                if (!string.IsNullOrEmpty(displayName))
                                {
                                    displayName = char.ToUpper(displayName[0]) + displayName.Substring(1);
                                }
                            }
                            catch { }

                            string buttonText = displayName.Split('(')[0].Trim();
                            languageButton.Text = $"{buttonText} ▾";
                        }
                    }
                }
            }
            catch { }
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
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            if (status == PermissionStatus.Granted)
            {
                // Khi quyền được cấp, Map sẽ tự động hiển thị vị trí
                if (this.FindByName<Microsoft.Maui.Controls.Maps.Map>("MyMap") is { } map)
                {
                    map.IsShowingUser = true;

                    try
                    {
                        var location = await Geolocation.GetLastKnownLocationAsync() ?? await Geolocation.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)));
                        if (location != null)
                        {
                            map.MoveToRegion(Microsoft.Maui.Maps.MapSpan.FromCenterAndRadius(new Microsoft.Maui.Devices.Sensors.Location(location.Latitude, location.Longitude), Microsoft.Maui.Maps.Distance.FromKilometers(1)));

                            AddDummyPinsToMap(map, location);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Xử lý lỗi nếu không lấy được vị trí
                        Console.WriteLine($"Lỗi lấy vị trí: {ex.Message}");
                    }
                }
            }
            else
            {
                await DisplayAlert("Quyền bị từ chối", "Không thể hiển thị vị trí của bạn vì thiếu quyền truy cập.", "OK");
            }
        }

        private async void AddDummyPinsToMap(Microsoft.Maui.Controls.Maps.Map map, Location userLocation)
        {
            map.Pins.Clear();

            // First check if database has POIs, if not, insert dummy ones
            var existingPois = await _database.GetPOIsAsync();
            if (existingPois == null || existingPois.Count == 0)
            {
                var dummyPois = new List<PointOfInterest>
                {
                    new PointOfInterest { Name = "Bún bò Huế Cô Ba", Description = "Quán bún bò nổi tiếng hơn 30 năm", Latitude = userLocation.Latitude + 0.002, Longitude = userLocation.Longitude + 0.002 },
                    new PointOfInterest { Name = "Đại Nội Huế", Description = "Hoàng thành lịch sử", Latitude = userLocation.Latitude - 0.003, Longitude = userLocation.Longitude + 0.001 },
                    new PointOfInterest { Name = "Cafe Muối", Description = "Đặc sản đồ uống nổi tiếng", Latitude = userLocation.Latitude + 0.001, Longitude = userLocation.Longitude - 0.004 }
                };

                foreach (var poi in dummyPois)
                {
                    await _database.SavePOIAsync(poi);
                }

                existingPois = await _database.GetPOIsAsync();
            }

            var nearbyList = this.FindByName<VerticalStackLayout>("NearbyPlacesList");
            var emptyLabel = this.FindByName<Label>("EmptyNearbyLabel");

            if (emptyLabel != null && existingPois.Count > 0)
            {
                emptyLabel.IsVisible = false;
            }

            if (nearbyList != null)
            {
                nearbyList.Children.Clear();
            }

            foreach (var poi in existingPois)
            {
                var pin = new Microsoft.Maui.Controls.Maps.Pin
                {
                    Label = poi.Name,
                    Address = poi.Description,
                    Type = Microsoft.Maui.Controls.Maps.PinType.Place,
                    Location = new Location(poi.Latitude, poi.Longitude)
                };

                pin.MarkerClicked += (s, args) =>
                {
                    OnPlaceSelected(poi.Name, poi.Description, "Bản đồ");
                };

                map.Pins.Add(pin);

                if (nearbyList != null)
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
                    contentLayout.Children.Add(new Label { Text = pin.Label, TextColor = Colors.White, FontAttributes = FontAttributes.Bold, FontSize = 16 });
                    contentLayout.Children.Add(new Label { Text = pin.Address, TextColor = Color.FromArgb("#A0938A"), FontSize = 13 });

                    itemLayout.Content = contentLayout;
                    nearbyList.Children.Add(itemLayout);
                }
            }
        }

        private bool isExpanded = false;

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

            // Reset tất cả tab headers
            if(lblTab1 != null) lblTab1.TextColor = Color.FromArgb("#A0938A");
            if(lineTab1 != null) lineTab1.Color = Color.FromArgb("#3A2121");
            if(lblTab2 != null) lblTab2.TextColor = Color.FromArgb("#A0938A");
            if(lineTab2 != null) lineTab2.Color = Color.FromArgb("#3A2121");
            if(lblTab3 != null) lblTab3.TextColor = Color.FromArgb("#A0938A");
            if(lineTab3 != null) lineTab3.Color = Color.FromArgb("#3A2121");

            // Ẩn tất cả Tab Content
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

        private async void OnQRScannerClicked(object sender, EventArgs e)
        {
            try
            {
                // Request camera permission
                var cameraStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
                if (cameraStatus != PermissionStatus.Granted)
                {
                    cameraStatus = await Permissions.RequestAsync<Permissions.Camera>();
                }

                if (cameraStatus == PermissionStatus.Granted)
                {
                    await Shell.Current.GoToAsync("qrscanner");
                }
                else
                {
                    await DisplayAlert("Quyền bị từ chối", "Cần cấp quyền truy cập camera để quét mã QR.", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", $"Không thể mở camera: {ex.Message}", "OK");
            }
        }

        private async Task HandleQRCodeResult(string qrId)
        {
            try
            {
                if (int.TryParse(qrId, out int id))
                {
                    var qrCode = await _database.GetQRCodesAsync();
                    var scannedQR = qrCode.FirstOrDefault(q => q.Id == id);

                    if (scannedQR != null)
                    {
                        // Update Tab 1 with QR code data
                        UpdateTab1Content("Quét mã QR", scannedQR.Name, scannedQR.Description);

                        // Select Tab 1
                        UpdateTabVisuals(1);

                        // Expand the drawer if not already expanded
                        ExpandDrawerIfNeeded();
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", $"Không thể xử lý mã QR: {ex.Message}", "OK");
            }
        }

        private async Task HandleLanguageSelection(string languageCode, string displayName)
        {
            try
            {
                if (_availableLocales == null)
                {
                    _availableLocales = await TextToSpeech.Default.GetLocalesAsync();
                }

                _selectedLanguageCode = languageCode;

                // Cập nhật nút hiển thị ngôn ngữ
                var languageButton = this.FindByName<Button>("LanguageButton");
                if (languageButton != null)
                {
                    languageButton.Text = $"{displayName} ▾";
                }

                // Tự động chọn giọng đọc đầu tiên của ngôn ngữ này
                _selectedLocale = _availableLocales?.FirstOrDefault(x => x.Language == _selectedLanguageCode);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Lỗi", $"Không thể cập nhật ngôn ngữ: {ex.Message}", "OK");
            }
        }

        private CancellationTokenSource _ttsCts;
        private bool _isPlaying = false;
        private Locale _selectedLocale;
        private IEnumerable<Locale> _availableLocales;
        private string _selectedLanguageCode;

        private async void OnAddLanguageClicked(object sender, EventArgs e)
        {
            try
            {
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
                if (_availableLocales == null)
                {
                    _availableLocales = await TextToSpeech.Default.GetLocalesAsync();
                }

                if (string.IsNullOrEmpty(_selectedLanguageCode))
                {
                    // Nếu chưa chọn ngôn ngữ, lấy ngôn ngữ của giọng đầu tiên làm mặc định
                    var defaultLocale = _availableLocales?.FirstOrDefault();
                    if (defaultLocale != null)
                    {
                        _selectedLanguageCode = defaultLocale.Language;
                        _selectedLocale = defaultLocale;
                    }
                    else
                    {
                        await DisplayAlert("Thông báo", "Không tìm thấy giọng nói nào trên thiết bị.", "OK");
                        return;
                    }
                }

                var voicesForLanguage = _availableLocales.Where(x => x.Language == _selectedLanguageCode).ToList();

                if (!voicesForLanguage.Any())
                {
                    await DisplayAlert("Thông báo", "Không tìm thấy giọng đọc nào cho ngôn ngữ này.", "OK");
                    return;
                }

                var voiceNames = voicesForLanguage.Select(x => x.Name).ToArray();
                string action = await DisplayActionSheet($"Chọn giọng ({_selectedLanguageCode})", "Đóng", null, voiceNames);

                if (action != null && action != "Đóng")
                {
                    _selectedLocale = voicesForLanguage.FirstOrDefault(x => x.Name == action);
                    await DisplayAlert("Thành công", $"Đã chọn giọng: {_selectedLocale?.Name}", "OK");
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

            var playPauseIcon = this.FindByName<Label>("PlayPauseIcon");
            var descriptionLabel = this.FindByName<Label>("DescriptionLabel");

            if (_isPlaying)
            {
                // Stop TTS
                if (_ttsCts != null)
                {
                    _ttsCts.Cancel();
                    _ttsCts.Dispose();
                    _ttsCts = null;
                }

                if (playPauseIcon != null) playPauseIcon.Text = "▶";
                _isPlaying = false;
            }
            else
            {
                if (descriptionLabel == null || string.IsNullOrWhiteSpace(descriptionLabel.Text))
                    return;

                if (playPauseIcon != null) playPauseIcon.Text = "⏸";
                _isPlaying = true;

                _ttsCts = new CancellationTokenSource();

                try
                {
                    var options = new SpeechOptions();
                    string textToSpeak = descriptionLabel.Text;

                    if (_selectedLocale != null)
                    {
                        options.Locale = _selectedLocale;

                        // Chờ dịch văn bản nếu ngôn ngữ khác tiếng Việt
                        textToSpeak = await TranslateTextAsync(textToSpeak, _selectedLocale.Language);
                    }

                    // Kiểm tra huỷ yêu cầu (nếu người dùng bấm nút Dừng khi đang dịch)
                    _ttsCts.Token.ThrowIfCancellationRequested();

                    await TextToSpeech.Default.SpeakAsync(textToSpeak, options, cancelToken: _ttsCts.Token);
                }
                catch (TaskCanceledException) { }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi TTS: {ex.Message}");
                }
                finally
                {
                    if (_isPlaying)
                    {
                        if (playPauseIcon != null) playPauseIcon.Text = "▶";
                        _isPlaying = false;
                    }
                }
            }
        }

        private async Task<string> TranslateTextAsync(string text, string targetLangCode)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(targetLangCode))
                return text;

            try
            {
                // Lấy đúng mã 2 ký tự (VD: "en", "ja", "fr")
                string langCode = targetLangCode;
                try
                {
                    langCode = new System.Globalization.CultureInfo(targetLangCode).TwoLetterISOLanguageName.ToLower();
                }
                catch
                {
                    langCode = targetLangCode.Split('-')[0].ToLower();
                }

                if (langCode == "vi") return text; // Không cần dịch nếu là tiếng Việt

                string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=vi&tl={langCode}&dt=t&q={Uri.EscapeDataString(text)}";

                using HttpClient client = new HttpClient();
                // Bổ sung User-Agent để không bị Google chặn
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                string response = await client.GetStringAsync(url);

                var jsonDoc = System.Text.Json.JsonDocument.Parse(response);
                var root = jsonDoc.RootElement;
                string translatedResult = "";

                if (root.ValueKind == System.Text.Json.JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    var firstItem = root[0];
                    if (firstItem.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var item in firstItem.EnumerateArray())
                        {
                            if (item.ValueKind == System.Text.Json.JsonValueKind.Array && item.GetArrayLength() > 0)
                            {
                                translatedResult += item[0].GetString();
                            }
                        }
                    }
                }

                return !string.IsNullOrWhiteSpace(translatedResult) ? translatedResult : text;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi dịch thuật: {ex.Message}");
                return text; // Trả về văn bản gốc nếu lỗi
            }
        }

        private void UpdateTab1Content(string source, string name, string description)
        {
            // Dừng phát TTS nếu đang đọc
            if (_isPlaying)
            {
                if (_ttsCts != null)
                {
                    _ttsCts.Cancel();
                    _ttsCts.Dispose();
                    _ttsCts = null;
                }
                var playPauseIcon = this.FindByName<Label>("PlayPauseIcon");
                if (playPauseIcon != null) playPauseIcon.Text = "▶";
                _isPlaying = false;
            }

            var tab1Content = this.FindByName<Border>("Tab1Content");
            if (tab1Content == null)
                return;

            var statusHStack = tab1Content.Content as VerticalStackLayout;
            if (statusHStack != null && statusHStack.Children.Count > 0)
            {
                // Update status label
                if (statusHStack.Children[0] is HorizontalStackLayout statusLayout && statusLayout.Children.Count > 1)
                {
                    if (statusLayout.Children[1] is Label statusLabel)
                    {
                        statusLabel.Text = $"{source} · {name}";
                    }
                }

                // Update title
                if (statusHStack.Children.Count > 1 && statusHStack.Children[1] is Label titleLabel)
                {
                    titleLabel.Text = name;
                }

                // Update description
                if (statusHStack.Children.Count > 2 && statusHStack.Children[2] is Label descriptionLabel)
                {
                    descriptionLabel.Text = description;
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

        private void OnPlaceSelected(string name, string description, string source)
        {
            UpdateTab1Content(source, name, description);
            UpdateTabVisuals(1);
            ExpandDrawerIfNeeded();
        }
    }
}
