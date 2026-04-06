using System.Globalization;
using System.Text.Json;
using ThuyetMinhTuDong.Data;

namespace ThuyetMinhTuDong.Services
{
    /// <summary>
    /// Service for handling Text-to-Speech operations including language selection,
    /// locale management, and text translation with SQLite caching + offline dictionary.
    /// </summary>
    public class TTSService
    {
        private CancellationTokenSource _ttsCts;
        private IEnumerable<Locale> _availableLocales;
        private string _selectedLanguageCode;
        private Locale _selectedLocale;
        private bool _isPlaying;

        private readonly LocalDatabase _database;
        private Dictionary<string, Dictionary<string, string>> _offlineDict;

        public event EventHandler<LanguageChangedEventArgs> LanguageChanged;
        public event EventHandler PlayStarted;
        public event EventHandler PlayStopped;

        public bool IsPlaying => _isPlaying;
        public string SelectedLanguageCode => _selectedLanguageCode;
        public Locale SelectedLocale => _selectedLocale;
        public IEnumerable<Locale> AvailableLocales => _availableLocales;

        public TTSService(LocalDatabase database = null)
        {
            _database = database;
        }

        /// <summary>
        /// Initializes TTS service and loads available locales, offline dictionary, and cache.
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                if (_availableLocales != null && _availableLocales.Any())
                    return;

                _availableLocales = await TextToSpeech.Default.GetLocalesAsync();

                // Initialize offline dictionary
                await InitializeOfflineDictionaryAsync();

                // Cleanup old cache
                if (_database != null)
                {
                    await _database.CleanupOldCacheAsync(daysOld: 30);
                    var cacheCount = await _database.GetCacheSizeAsync();
                    System.Diagnostics.Debug.WriteLine($"[TTS] Cache initialized: {cacheCount} items");
                }

                if (_availableLocales != null && _availableLocales.Any())
                {
                    if (!string.IsNullOrWhiteSpace(_selectedLanguageCode))
                    {
                        _selectedLocale = _availableLocales.FirstOrDefault(x =>
                            string.Equals(x.Language, _selectedLanguageCode, StringComparison.OrdinalIgnoreCase));

                        if (_selectedLocale == null)
                        {
                            var normalized = _selectedLanguageCode.Split('-')[0];
                            _selectedLocale = _availableLocales.FirstOrDefault(x =>
                                x.Language.StartsWith(normalized, StringComparison.OrdinalIgnoreCase));
                        }

                        if (_selectedLocale != null)
                        {
                            _selectedLanguageCode = _selectedLocale.Language;
                            return;
                        }
                    }

                    _selectedLocale = _availableLocales.FirstOrDefault(x =>
                        x.Language.StartsWith("vi", StringComparison.OrdinalIgnoreCase))
                        ?? _availableLocales.FirstOrDefault();

                    _selectedLanguageCode = _selectedLocale?.Language;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TTS Initialization Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets the language and updates the selected locale.
        /// </summary>
        public void SetLanguage(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode) || _availableLocales == null)
                return;

            _selectedLanguageCode = languageCode;

            _selectedLocale = _availableLocales.FirstOrDefault(x =>
                string.Equals(x.Language, languageCode, StringComparison.OrdinalIgnoreCase));

            if (_selectedLocale == null)
            {
                var normalizedLanguage = languageCode.Split('-')[0];
                _selectedLocale = _availableLocales.FirstOrDefault(x =>
                    x.Language.StartsWith(normalizedLanguage, StringComparison.OrdinalIgnoreCase));

                if (_selectedLocale != null)
                {
                    _selectedLanguageCode = _selectedLocale.Language;
                }
            }

            LanguageChanged?.Invoke(this, new LanguageChangedEventArgs { LanguageCode = _selectedLanguageCode });
        }

        /// <summary>
        /// Gets the display name for a language code.
        /// </summary>
        public string GetLanguageDisplayName(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode))
                return string.Empty;

            try
            {
                var culture = new CultureInfo(languageCode);
                string displayName = culture.NativeName;

                if (!string.IsNullOrEmpty(displayName))
                {
                    displayName = char.ToUpper(displayName[0]) + displayName.Substring(1);
                    return displayName.Split('(')[0].Trim();
                }
            }
            catch { }

            return languageCode;
        }

        /// <summary>
        /// Gets available voices for the specified language.
        /// </summary>
        public List<Locale> GetVoicesForLanguage(string languageCode)
        {
            if (_availableLocales == null || string.IsNullOrWhiteSpace(languageCode))
                return new List<Locale>();

            return _availableLocales.Where(x => x.Language == languageCode).ToList();
        }

        /// <summary>
        /// Selects a specific voice/locale for TTS.
        /// </summary>
        public void SetVoice(Locale locale)
        {
            if (locale != null)
            {
                _selectedLocale = locale;
                _selectedLanguageCode = locale.Language;
                LanguageChanged?.Invoke(this, new LanguageChangedEventArgs { LanguageCode = locale.Language });
            }
        }

        /// <summary>
        /// Speaks the given text asynchronously.
        /// </summary>
        public async Task SpeakAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            StopSpeaking();

            _ttsCts = new CancellationTokenSource();
            _isPlaying = true;
            PlayStarted?.Invoke(this, EventArgs.Empty);

            try
            {
                var options = new SpeechOptions();

                if (_selectedLocale != null)
                {
                    options.Locale = _selectedLocale;
                }

                string selectedLanguage = _selectedLanguageCode ?? _selectedLocale?.Language;
                string textToSpeak = text;

                if (!string.IsNullOrWhiteSpace(selectedLanguage) &&
                    !selectedLanguage.StartsWith("vi", StringComparison.OrdinalIgnoreCase))
                {
                    textToSpeak = await TranslateTextAsync(text, selectedLanguage);
                }

                _ttsCts.Token.ThrowIfCancellationRequested();
                await TextToSpeech.Default.SpeakAsync(textToSpeak, options, cancelToken: _ttsCts.Token);
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TTS Error: {ex.Message}");
            }
            finally
            {
                _isPlaying = false;
                PlayStopped?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Stops the current TTS playback.
        /// </summary>
        public void StopSpeaking()
        {
            if (_ttsCts != null)
            {
                _ttsCts.Cancel();
                _ttsCts.Dispose();
                _ttsCts = null;
            }

            _isPlaying = false;
            PlayStopped?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Translates text from Vietnamese to the target language using Google Translate API,
        /// with offline dictionary and SQLite cache fallback.
        /// </summary>
        public async Task<string> TranslateTextAsync(string text, string targetLangCode)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            // Vietnamese = no translation needed
            if (targetLangCode.StartsWith("vi", StringComparison.OrdinalIgnoreCase))
                return text;

            // Step 1: Offline Dictionary
            var offlineResult = TryGetOfflineTranslation(text, targetLangCode);
            if (offlineResult != null)
            {
                System.Diagnostics.Debug.WriteLine($"[Dictionary HIT] {text} -> {offlineResult}");
                return offlineResult;
            }

            // Step 2: SQLite Cache
            if (_database != null)
            {
                var cached = await _database.GetTranslationAsync(text, targetLangCode);
                if (cached != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[Cache HIT] {text} -> {cached.TranslatedText}");
                    return cached.TranslatedText;
                }
            }

            // Step 3: Google Translate API
            var translated = await TranslateViaApiAsync(text, targetLangCode);

            // Save to cache if translation successful
            if (!string.IsNullOrWhiteSpace(translated) && translated != text && _database != null)
            {
                _ = _database.SaveTranslationAsync(text, targetLangCode, translated);
            }

            return translated;
        }

        private async Task InitializeOfflineDictionaryAsync()
        {
            try
            {
                var jsonPath = Path.Combine(FileSystem.AppDataDirectory, "translations.json");
                
                if (!File.Exists(jsonPath))
                {
                    await CreateDefaultOfflineDictionaryAsync(jsonPath);
                }

                var json = await File.ReadAllTextAsync(jsonPath);
                _offlineDict = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);
                
                System.Diagnostics.Debug.WriteLine($"[Dictionary] Loaded {_offlineDict?.Count ?? 0} language pairs");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Dictionary init error: {ex.Message}");
                _offlineDict = new();
            }
        }

        private async Task CreateDefaultOfflineDictionaryAsync(string jsonPath)
        {
            try
            {
                var defaultDict = new Dictionary<string, Dictionary<string, string>>
                {
                    {
                        "vi_en", new Dictionary<string, string>
                        {
                            { "Hồ Hoàn Kiếm", "Hoan Kiem Lake" },
                            { "Bến thuyền Trùng Dùng", "Trung Dung Wharf" },
                            { "Cầu Mùng", "Mung Bridge" },
                            { "Công viên vẻn sông", "Riverside Park" },
                            { "Nhà hàng hải sản Sông Xanh", "Song Xanh Seafood Restaurant" },
                            { "Đài quan sát trung tâm", "Central Observatory Tower" },
                            { "Bãi cổ picnic", "Ancient Picnic Area" },
                            { "Nhà vệ sinh công cộng", "Public Restroom" },
                            { "Trạm xe buyt Bến Nghé", "Ben Nghe Bus Station" },
                            { "Bãi giữ xe an toàn", "Safe Parking Area" },
                            { "Quầy bán vé", "Ticket Counter" },
                            { "Quầy nước giải khát", "Beverage Counter" },
                            { "ATM", "ATM Machine" }
                        }
                    },
                    {
                        "vi_fr", new Dictionary<string, string>
                        {
                            { "Hồ Hoàn Kiếm", "Lac Hoan Kiem" },
                            { "Bến thuyền Trùng Dùng", "Quai de Trung Dung" },
                            { "Cầu Mùng", "Pont Mung" }
                        }
                    },
                    {
                        "vi_ja", new Dictionary<string, string>
                        {
                            { "Hồ Hoàn Kiếm", "ホアンキエム湖" },
                            { "Bến thuyền Trùng Dùng", "トゥンズン埠頭" }
                        }
                    }
                };

                var json = JsonSerializer.Serialize(defaultDict, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(jsonPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Create default dictionary error: {ex.Message}");
            }
        }

        private string TryGetOfflineTranslation(string text, string targetLang)
        {
            if (_offlineDict == null || string.IsNullOrWhiteSpace(text))
                return null;

            string dictionaryKey = $"vi_{targetLang.Split('-')[0].ToLower()}";

            if (_offlineDict.TryGetValue(dictionaryKey, out var translations))
            {
                if (translations.TryGetValue(text, out var translated))
                {
                    return translated;
                }
            }

            return null;
        }

        private async Task<string> TranslateViaApiAsync(string text, string targetLangCode)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            try
            {
                var langCode = targetLangCode.Split('-')[0].ToLower();
                string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=vi&tl={langCode}&dt=t&q={Uri.EscapeDataString(text)}";

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using HttpClient client = new();
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

                string response = await client.GetStringAsync(url, cts.Token);
                var translated = ParseGoogleTranslateResponse(response);

                if (!string.IsNullOrWhiteSpace(translated))
                {
                    System.Diagnostics.Debug.WriteLine($"[API] Translated: {text} -> {translated}");
                    return translated;
                }
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[API] Timeout - Google Translate");
            }
            catch (HttpRequestException)
            {
                System.Diagnostics.Debug.WriteLine("[API] No internet connection");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Error: {ex.Message}");
            }

            // Fallback: return original text
            return text;
        }

        private string ParseGoogleTranslateResponse(string response)
        {
            try
            {
                var jsonDoc = JsonDocument.Parse(response);
                var root = jsonDoc.RootElement;
                string result = "";

                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    var firstItem = root[0];
                    if (firstItem.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in firstItem.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.Array && item.GetArrayLength() > 0)
                            {
                                result += item[0].GetString();
                            }
                        }
                    }
                }
                return result;
            }
            catch
            {
                return null;
            }
        }

        public class LanguageChangedEventArgs : EventArgs
        {
            public string LanguageCode { get; set; }
        }
    }
}
