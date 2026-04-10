using System.Globalization;

namespace ThuyetMinhTuDong.Services
{
    /// <summary>
    /// Service for handling Text-to-Speech operations including language selection,
    /// locale management, and delegated translation.
    /// </summary>
    public class TTSService
    {
        private CancellationTokenSource _ttsCts;
        private IEnumerable<Locale> _availableLocales;
        private string _selectedLanguageCode;
        private Locale _selectedLocale;
        private bool _isPlaying;

        private readonly ITranslateService _translateService;

        public event EventHandler<LanguageChangedEventArgs> LanguageChanged;
        public event EventHandler PlayStarted;
        public event EventHandler PlayStopped;
        public event EventHandler<string> TranslationStarted;
        public event EventHandler TranslationFinished;

        public bool IsPlaying => _isPlaying;
        public string SelectedLanguageCode => _selectedLanguageCode;
        public Locale SelectedLocale => _selectedLocale;
        public IEnumerable<Locale> AvailableLocales => _availableLocales;

        public TTSService(ITranslateService translateService)
        {
            _translateService = translateService;
        }

        /// <summary>
        /// Initializes TTS service and loads available locales.
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                if (_availableLocales != null && _availableLocales.Any())
                    return;

                _availableLocales = await TextToSpeech.Default.GetLocalesAsync();
                await _translateService.InitializeAsync();

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

        public List<Locale> GetVoicesForLanguage(string languageCode)
        {
            if (_availableLocales == null || string.IsNullOrWhiteSpace(languageCode))
                return new List<Locale>();

            return _availableLocales.Where(x => x.Language == languageCode).ToList();
        }

        public void SetVoice(Locale locale)
        {
            if (locale != null)
            {
                _selectedLocale = locale;
                _selectedLanguageCode = locale.Language;
                LanguageChanged?.Invoke(this, new LanguageChangedEventArgs { LanguageCode = locale.Language });
            }
        }

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
                    string targetLangName = GetLanguageDisplayName(selectedLanguage);
                    TranslationStarted?.Invoke(this, targetLangName);
                    try
                    {
                        textToSpeak = await _translateService.TranslateTextAsync(text, selectedLanguage);
                    }
                    finally
                    {
                        TranslationFinished?.Invoke(this, EventArgs.Empty);
                    }
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

        public class LanguageChangedEventArgs : EventArgs
        {
            public string LanguageCode { get; set; }
        }
    }
}
