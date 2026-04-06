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
            await Task.Run(async () => 
            {
                var locales = await TextToSpeech.Default.GetLocalesAsync();

                if (locales == null || !locales.Any())
                {
                    MainThread.BeginInvokeOnMainThread(async () => 
                    {
                        await DisplayAlert("Thông báo", "Không tìm thấy ngôn ngữ nào trên thiết bị.", "OK");
                        await Shell.Current.GoToAsync("..");
                    });
                    return;
                }

                _allLocales = locales;
                var uniqueLanguages = _allLocales.Select(x => x.Language).Distinct().ToList();

                var tempCodeMap = new Dictionary<string, string>();

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
                    }

                    if (!tempCodeMap.ContainsKey(displayName))
                    {
                        tempCodeMap[displayName] = langCode;
                    }
                    else
                    {
                        string uniqueKey = $"{displayName} ({langCode})";
                        tempCodeMap[uniqueKey] = langCode;
                    }
                }

                var sortedLanguages = tempCodeMap.Keys.OrderBy(x => x).ToList();

                MainThread.BeginInvokeOnMainThread(() => 
                {
                    _languageCodeMap = tempCodeMap;
                    _displayedLanguages = new ObservableCollection<string>(sortedLanguages);
                    LanguagesList.ItemsSource = _displayedLanguages;
                });
            });
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
            var sortedLanguages = _languageCodeMap.Keys.OrderBy(x => x).ToList();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _displayedLanguages.Clear();
                foreach (var lang in sortedLanguages)
                {
                    _displayedLanguages.Add(lang);
                }
            });
        }
        else
        {
            var filtered = _languageCodeMap.Keys
                .Where(x => x.ToLower().Contains(searchText))
                .OrderBy(x => x)
                .ToList();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                _displayedLanguages.Clear();
                foreach (var lang in filtered)
                {
                    _displayedLanguages.Add(lang);
                }
            });
        }
    }

    private async void OnLanguageSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is string selectedLanguage)
        {
            // Lấy mã ngôn ngữ từ dictionary
            if (_languageCodeMap.TryGetValue(selectedLanguage, out var languageCode))
            {
                // Reset selection trước khi navigate
                ((CollectionView)sender).SelectedItem = null;

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
