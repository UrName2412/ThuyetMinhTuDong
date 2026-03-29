using System.Collections.ObjectModel;

namespace ThuyetMinhTuDong;

public partial class LanguageSearchPage : ContentPage
{
    private IEnumerable<Locale> _allLocales;
    private ObservableCollection<string> _displayedLanguages;
    private Dictionary<string, string> _languageCodeMap;

    public LanguageSearchPage()
    {
        InitializeComponent();
        _displayedLanguages = new ObservableCollection<string>();
        _languageCodeMap = new Dictionary<string, string>();
        LanguagesList.ItemsSource = _displayedLanguages;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            _allLocales = await TextToSpeech.Default.GetLocalesAsync();

            if (_allLocales == null || !_allLocales.Any())
            {
                await DisplayAlert("Thông báo", "Không tìm thấy ngôn ngữ nào trên thiết bị.", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            // Lấy danh sách các mã ngôn ngữ duy nhất
            var uniqueLanguages = _allLocales.Select(x => x.Language).Distinct().ToList();

            // Tạo display name và sắp xếp theo tên hiển thị
            foreach (var langCode in uniqueLanguages)
            {
                string displayName = langCode;
                try
                {
                    var culture = new System.Globalization.CultureInfo(langCode);
                    displayName = culture.NativeName;
                    if (!string.IsNullOrEmpty(displayName))
                    {
                        displayName = char.ToUpper(displayName[0]) + displayName.Substring(1);
                    }
                }
                catch
                {
                    // Giữ nguyên mã nếu không parse được
                }

                // Xử lý trường hợp trùng tên
                if (!_languageCodeMap.ContainsKey(displayName))
                {
                    _languageCodeMap[displayName] = langCode;
                }
                else
                {
                    string uniqueKey = $"{displayName} ({langCode})";
                    _languageCodeMap[uniqueKey] = langCode;
                }
            }

            // Sắp xếp theo tên hiển thị
            var sortedLanguages = _languageCodeMap.Keys.OrderBy(x => x).ToList();

            _displayedLanguages.Clear();
            foreach (var lang in sortedLanguages)
            {
                _displayedLanguages.Add(lang);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", $"Không thể tải danh sách ngôn ngữ: {ex.Message}", "OK");
            await Shell.Current.GoToAsync("..");
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = e.NewTextValue?.ToLower() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(searchText))
        {
            // Hiển thị tất cả nếu search box trống
            var sortedLanguages = _languageCodeMap.Keys.OrderBy(x => x).ToList();
            _displayedLanguages.Clear();
            foreach (var lang in sortedLanguages)
            {
                _displayedLanguages.Add(lang);
            }
        }
        else
        {
            // Lọc theo text tìm kiếm
            var filtered = _languageCodeMap.Keys
                .Where(x => x.ToLower().Contains(searchText))
                .OrderBy(x => x)
                .ToList();

            _displayedLanguages.Clear();
            foreach (var lang in filtered)
            {
                _displayedLanguages.Add(lang);
            }
        }
    }

    private async void OnLanguageSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is string selectedLanguage)
        {
            // Lấy mã ngôn ngữ từ dictionary
            if (_languageCodeMap.TryGetValue(selectedLanguage, out var languageCode))
            {
                // Truyền dữ liệu về MainPage thông qua Navigation Parameters
                await Shell.Current.GoToAsync($"..", new Dictionary<string, object>
                {
                    { "selectedLanguageCode", languageCode },
                    { "selectedLanguageDisplay", selectedLanguage.Split('(')[0].Trim() }
                });
            }
        }
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
