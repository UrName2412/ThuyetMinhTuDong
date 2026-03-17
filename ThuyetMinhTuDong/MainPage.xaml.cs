using ThuyetMinhTuDong.Data;
using ThuyetMinhTuDong.Models;

namespace ThuyetMinhTuDong
{
    public partial class MainPage : ContentPage
    {
        private readonly LocalDatabase _database;

        public MainPage(LocalDatabase database)
        {
            InitializeComponent();
            _database = database;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await CheckAndRequestLocationPermission();

            // Tối ưu: Bật định vị khi trang xuất hiện
            if (this.FindByName<Microsoft.Maui.Controls.Maps.Map>("MyMap") is { } map)
            {
                map.IsShowingUser = true;
            }
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

        private async void OnAddLanguageClicked(object sender, EventArgs e)
        {
            string action = await DisplayActionSheet("Chọn ngôn ngữ mới", "Đóng", null,
               "Japanese(JP)",
               "English(EN)",
               "Vietnamese(VI)",
               "Spanish(ES)",
               "French(FR)",
               "German(DE)",
               "Korean(KR)",
               "Russian(RU)",
               "Portuguese(PT)"
            );

            if (action != null && action != "Đóng")
            {
                string btnText = action;

                // Format text from "Japanese(JP)" to "JP Japanese"
                var match = System.Text.RegularExpressions.Regex.Match(action, @"(.*?)\((.*?)\)");
                if (match.Success)
                {
                    btnText = $"{match.Groups[2].Value} {match.Groups[1].Value}";
                }

                // Cập nhật text cho single button picker
                var languageButton = this.FindByName<Button>("LanguageButton");
                if (languageButton != null)
                {
                    languageButton.Text = $"{btnText} ▾";
                }
            }
        }
    }
}
