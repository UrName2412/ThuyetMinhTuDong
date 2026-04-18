using System.Collections.ObjectModel;
using ThuyetMinhTuDong.Services;

namespace ThuyetMinhTuDong;

[QueryProperty(nameof(Purpose), "purpose")]
public partial class LanguageSearchPage : ContentPage
{
    private IEnumerable<Locale> _allLocales;
    private ObservableCollection<string> _displayedLanguages;
    private Dictionary<string, string> _languageCodeMap;

    private string _purpose = "Audio"; // Audio or UI
    public string Purpose
    {
        get => _purpose;
        set => _purpose = value;
    }

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
            var translateService = App.Current?.Handler?.MauiContext?.Services.GetService<ITranslateService>();
            if (translateService != null)
            {
                var langCode = Preferences.Default.Get("UiLanguageCode", "vi");
                var translatedTitle = await translateService.TranslateTextAsync("Chọn ngôn ngữ", langCode);
                var translatedPlaceholder = await translateService.TranslateTextAsync("Tìm kiếm ngôn ngữ...", langCode);

                MainThread.BeginInvokeOnMainThread(() => 
                {
                    if (MyTitleLabel != null) MyTitleLabel.Text = translatedTitle;
                    Title = translatedTitle;

                    if (SearchLanguageBar != null) SearchLanguageBar.Placeholder = translatedPlaceholder;
                });
            }
        }
        catch { }

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

                var display = selectedLanguage.Split('(')[0].Trim();

                var navParameters = new Dictionary<string, object>();
                if (string.Equals(Purpose, "UI", StringComparison.OrdinalIgnoreCase))
                {
                    navParameters.Add("uiLanguageCode", languageCode);
                    navParameters.Add("uiLanguageDisplay", display);
                }
                else
                {
                    navParameters.Add("selectedLanguageCode", languageCode);
                    navParameters.Add("selectedLanguageDisplay", display);
                }

                // Truyền dữ liệu về MainPage thông qua Navigation Parameters
                await Shell.Current.GoToAsync($"..", navParameters);
            }
        }
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
