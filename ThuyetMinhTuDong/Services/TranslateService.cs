using System.Text.Json;
using ThuyetMinhTuDong.Data;

namespace ThuyetMinhTuDong.Services
{
    public class TranslateService : ITranslateService
    {
        private readonly LocalDatabase? _database;
        private Dictionary<string, Dictionary<string, string>> _offlineDict = new();

        public TranslateService(LocalDatabase? database = null)
        {
            _database = database;
        }

        public async Task InitializeAsync()
        {
            await InitializeOfflineDictionaryAsync();

            if (_database != null)
            {
                await _database.CleanupOldCacheAsync(daysOld: 30);
            }
        }

        public async Task<string> TranslateTextAsync(string text, string targetLangCode)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            if (targetLangCode.StartsWith("vi", StringComparison.OrdinalIgnoreCase))
                return text;

            var offlineResult = TryGetOfflineTranslation(text, targetLangCode);
            if (offlineResult != null)
            {
                System.Diagnostics.Debug.WriteLine($"[Dictionary HIT] {text} -> {offlineResult}");
                return offlineResult;
            }

            if (_database != null)
            {
                var cached = await _database.GetTranslationAsync(text, targetLangCode);
                if (cached != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[Cache HIT] {text} -> {cached.TranslatedText}");
                    return cached.TranslatedText;
                }
            }

            var translated = await TranslateViaApiAsync(text, targetLangCode);

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
                _offlineDict = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json) ?? new();
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
                            { "Công viên vẻn sông", "Riverside Park" }
                        }
                    },
                    {
                        "vi_fr", new Dictionary<string, string>
                        {
                            { "Hồ Hoàn Kiếm", "Lac Hoan Kiem" }
                        }
                    },
                    {
                        "vi_ja", new Dictionary<string, string>
                        {
                            { "Hồ Hoàn Kiếm", "ホアンキエム湖" }
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

        private string? TryGetOfflineTranslation(string text, string targetLang)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            string dictionaryKey = $"vi_{targetLang.Split('-')[0].ToLower()}";

            if (_offlineDict.TryGetValue(dictionaryKey, out var translations) &&
                translations.TryGetValue(text, out var translated))
            {
                return translated;
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
                    return translated;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Error: {ex.Message}");
            }

            return text;
        }

        private string? ParseGoogleTranslateResponse(string response)
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
    }
}
