using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using ThuyetMinhTuDong.Data;
using ThuyetMinhTuDong.Models;

namespace ThuyetMinhTuDong.Services
{
    /// <summary>
    /// Service for managing Points of Interest (Places) with database operations.
    /// </summary>
    public class PlaceService
    {
        private readonly LocalDatabase _database;
        private List<PointOfInterest> _cachedPois;
        private static readonly HttpClient _httpClient = new HttpClient();

        public PlaceService(LocalDatabase database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        /// <summary>
        /// Gets all points of interest from the database.
        /// Uses caching to reduce database calls.
        /// </summary>
        public async Task<List<PointOfInterest>> GetAllPOIsAsync(bool forceRefresh = false)
        {
            try
            {
                if (!forceRefresh && _cachedPois != null)
                    return _cachedPois;

                _cachedPois = await _database.GetPOIsAsync();
                return _cachedPois ?? new List<PointOfInterest>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Get POIs Error: {ex.Message}");
                return new List<PointOfInterest>();
            }
        }

        /// <summary>
        /// Saves a point of interest to the database.
        /// </summary>
        public async Task<int> SavePOIAsync(PointOfInterest poi)
        {
            try
            {
                if (poi == null)
                    throw new ArgumentNullException(nameof(poi));

                int result = await _database.SavePOIAsync(poi);
                
                // Invalidate cache on successful save
                if (result > 0)
                    _cachedPois = null;

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save POI Error: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Saves multiple points of interest to the database.
        /// </summary>
        public async Task<bool> SavePOIsAsync(IEnumerable<PointOfInterest> pois)
        {
            try
            {
                if (pois == null || !pois.Any())
                    return false;

                int count = await _database.SavePOIsBatchAsync(pois);

                // Invalidate cache on successful saves
                if (count > 0)
                    _cachedPois = null;

                return count > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save POIs Batch Error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Initializes default sample POIs if the database is empty.
        /// </summary>
        public async Task EnsureDefaultPOIsAsync(Location userLocation)
        {
            try
            {
                var existingPois = await GetAllPOIsAsync();

                if (existingPois.Count == 0 && userLocation != null)
                {
                    var defaultPois = CreateDefaultPOIs(userLocation);
                    await SavePOIsAsync(defaultPois);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Initialize Default POIs Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates default sample POIs relative to the user's location.
        /// </summary>
        private List<PointOfInterest> CreateDefaultPOIs(Location userLocation)
        {
            return new List<PointOfInterest>
            {
                new PointOfInterest
                {
                    Name = "Bún bò Huế Cô Ba",
                    Description = "Quán bún bò nổi tiếng hơn 30 năm",
                    Latitude = userLocation.Latitude + 0.002,
                    Longitude = userLocation.Longitude + 0.002
                },
                new PointOfInterest
                {
                    Name = "Đại Nội Huế",
                    Description = "Hoàng thành lịch sử",
                    Latitude = userLocation.Latitude - 0.003,
                    Longitude = userLocation.Longitude + 0.001
                },
                new PointOfInterest
                {
                    Name = "Cafe Muối",
                    Description = "Đặc sản đồ uống nổi tiếng",
                    Latitude = userLocation.Latitude + 0.001,
                    Longitude = userLocation.Longitude - 0.004
                }
            };
        }

        /// <summary>
        /// Deletes a point of interest from the database.
        /// </summary>
        public async Task<bool> DeletePOIAsync(PointOfInterest poi)
        {
            try
            {
                if (poi == null)
                    return false;

                int result = await _database.DeletePOIAsync(poi);
                
                // Invalidate cache on successful delete
                if (result > 0)
                    _cachedPois = null;

                return result > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Delete POI Error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Converts POI to a Map Pin for display.
        /// </summary>
        public Microsoft.Maui.Controls.Maps.Pin CreateMapPin(PointOfInterest poi)
        {
            if (poi == null || string.IsNullOrWhiteSpace(poi.Name))
                return null;

            return new Microsoft.Maui.Controls.Maps.Pin
            {
                Label = poi.Name,
                Address = poi.Description ?? string.Empty,
                Type = Microsoft.Maui.Controls.Maps.PinType.Place,
                Location = new Location(poi.Latitude, poi.Longitude)
            };
        }

        /// <summary>
        /// Clears the cache to force next read from database.
        /// </summary>
        public void ClearCache()
        {
            _cachedPois = null;
        }

        /// <summary>
        /// Synchronizes POIs from the web API with soft delete support.
        /// Detects deleted POIs and updates local database accordingly.
        /// </summary>
        public async Task<bool> SyncPOIsFromApiAsync(string apiUrl, string apiKey = null)
        {
            if (string.IsNullOrWhiteSpace(apiUrl))
            {
                System.Diagnostics.Debug.WriteLine("[POI_SYNC] API URL is empty.");
                return false;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("[Sync] Starting POI sync...");

                var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                if (!string.IsNullOrWhiteSpace(apiKey) &&
                    !string.Equals(apiKey, "YOUR_SUPABASE_ANON_KEY", StringComparison.OrdinalIgnoreCase))
                {
                    request.Headers.Add("apikey", apiKey);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                }

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                var response = await _httpClient.SendAsync(request, cts.Token);

                System.Diagnostics.Debug.WriteLine($"[Sync] HTTP status: {(int)response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[Sync] HTTP error: {errorBody}");
                    return false;
                }

                var json = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[Sync] Received {json?.Length ?? 0} bytes");
                
                // ✅ DEBUG: Log first POI to see structure
                if (!string.IsNullOrEmpty(json))
                {
                    try
                    {
                        var firstPoiEnd = json.IndexOf("},") > 0 ? json.IndexOf("},") + 1 : json.Length - 1;
                        string firstPoi = json.Substring(0, Math.Min(500, firstPoiEnd));
                        System.Diagnostics.Debug.WriteLine($"[Sync] First POI sample: {firstPoi}");
                    }
                    catch { }
                }

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    System.Diagnostics.Debug.WriteLine("[Sync] Response is not an array.");
                    return false;
                }

                // Parse remote POIs
                var remotePois = JsonSerializer.Deserialize<List<PointOfInterest>>(json) ?? new();
                System.Diagnostics.Debug.WriteLine($"[Sync] Remote POIs: {remotePois.Count}");
                
                // ⚠️ DEBUG: Check if names are populated
                for (int i = 0; i < Math.Min(3, remotePois.Count); i++)
                {
                    var poi = remotePois[i];
                    System.Diagnostics.Debug.WriteLine($"[Sync] POI {i}: ID={poi.Id}, Name='{poi.Name}', Lat={poi.Latitude}, Long={poi.Longitude}");
                }
                
                // ✅ RE-ENABLE validation filter now that names are fixed
                var validRemotePois = remotePois.Where(p => !string.IsNullOrWhiteSpace(p.Name)).ToList();
                if (validRemotePois.Count != remotePois.Count)
                {
                    System.Diagnostics.Debug.WriteLine($"[Sync] WARNING: {remotePois.Count - validRemotePois.Count} remote POIs have empty names (filtered out)");
                }
                remotePois = validRemotePois;

                // Đồng bộ thông minh: Upsert POI thay vì xóa toàn bộ để tránh LocalId tăng liên tục
                var localPois = await _database.GetPOIsAsync();
                var remotePoiIds = remotePois.Select(p => p.Id).ToHashSet();

                // Xóa POI local không còn trong remote
                var poisToDelete = localPois.Where(p => !remotePoiIds.Contains(p.Id)).ToList();
                foreach (var poi in poisToDelete)
                {
                    await _database.DeletePOIAsync(poi);
                    System.Diagnostics.Debug.WriteLine($"[Sync] Deleted POI: {poi.Name} (ID: {poi.Id})");
                }

                // Upsert tất cả POI từ remote (cập nhật nếu tồn tại, thêm mới nếu không)
                if (remotePois.Any())
                {
                    await _database.SavePOIsBatchAsync(remotePois);
                    System.Diagnostics.Debug.WriteLine($"[Sync] 💾 Đã upsert thành công {remotePois.Count} POIs từ Remote.");
                }

                System.Diagnostics.Debug.WriteLine($"[Sync] Upsert completed: {remotePois.Count} remote POIs, {poisToDelete.Count} deleted locally");

                // Update sync time
                await _database.UpdateSyncTimeAsync("poi_last_sync", DateTime.Now);
                System.Diagnostics.Debug.WriteLine($"[Sync] Xong! Số lượng POI active thực tế: {remotePois.Count}");

                // Invalidate memory cache
                _cachedPois = null;

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Sync] Error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Hard deletes POIs that were soft-deleted more than specified days ago.
        /// Run periodically for database cleanup.
        /// </summary>
        public async Task CleanupSoftDeletedPOIsAsync(int daysOld = 90)
        {
            try
            {
                var allPois = await _database.GetPOIsAsync();
                var cutoffDate = DateTime.Now.AddDays(-daysOld);

                int deletedCount = 0;
                foreach (var poi in allPois.Where(x => x.IsDeleted && x.DeletedAt < cutoffDate))
                {
                    await _database.DeletePOIAsync(poi);
                    deletedCount++;
                    System.Diagnostics.Debug.WriteLine($"[Cleanup] Hard deleted: {poi.Name}");
                }

                System.Diagnostics.Debug.WriteLine($"[Cleanup] Permanently deleted {deletedCount} POIs");
                _cachedPois = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Cleanup] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets all active POIs (not soft-deleted).
        /// </summary>
        public async Task<List<PointOfInterest>> GetAllActivePOIsAsync(bool forceRefresh = false)
        {
            try
            {
                var allPois = await GetAllPOIsAsync(forceRefresh);
                return allPois.Where(x => !x.IsDeleted).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Get Active POIs Error: {ex.Message}");
                return new List<PointOfInterest>();
            }
        }

        /// <summary>
        /// Synchronizes POIs from the web API and updates the local database.
        /// </summary>
        public async Task<bool> SyncPOIsFromApiAsync_OLD(string apiUrl, string apiKey = null)
        {
            if (string.IsNullOrWhiteSpace(apiUrl))
            {
                System.Diagnostics.Debug.WriteLine("[POI_SYNC] API URL is empty.");
                return false;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"[POI_SYNC] Calling API: {apiUrl}");

                using var client = new HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(20)
                };

                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                if (!string.IsNullOrWhiteSpace(apiKey) &&
                    !string.Equals(apiKey, "YOUR_SUPABASE_ANON_KEY", StringComparison.OrdinalIgnoreCase))
                {
                    client.DefaultRequestHeaders.Remove("apikey");
                    client.DefaultRequestHeaders.Add("apikey", apiKey);
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                }

                var response = await client.GetAsync(apiUrl);

                System.Diagnostics.Debug.WriteLine($"[POI_SYNC] HTTP status: {(int)response.StatusCode} - {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[POI_SYNC] HTTP error body: {errorBody}");
                    return false;
                }

                var json = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[POI_SYNC] JSON length: {json?.Length ?? 0}");

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    System.Diagnostics.Debug.WriteLine("[POI_SYNC] JSON root is not an array.");
                    return false;
                }

                var pois = new List<PointOfInterest>();

                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    var name = GetString(item, "name", "poi_name", "title", "ten");
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    var latitude = GetDouble(item, "latitude", "lat");
                    var longitude = GetDouble(item, "longitude", "lng", "lon");

                    var description = GetString(item, "description", "desc", "mo_ta") ?? string.Empty;
                    var audioUrl = GetString(item, "audio_url", "audioUrl", "image_url");

                    pois.Add(new PointOfInterest
                    {
                        Name = name.Trim(),
                        Description = description,
                        Latitude = latitude,
                        Longitude = longitude,
                        AudioUrl = audioUrl
                    });
                }

                System.Diagnostics.Debug.WriteLine($"[POI_SYNC] Valid POIs after mapping: {pois.Count}");

                if (pois.Count == 0)
                    return false;

                await _database.DeleteAllPOIsAsync();
                var saveOk = await SavePOIsAsync(pois);
                _cachedPois = null;

                var localPois = await GetAllPOIsAsync(forceRefresh: true);
                System.Diagnostics.Debug.WriteLine($"[POI_SYNC] Save result: {saveOk}; Local DB POIs: {localPois.Count}");

                return saveOk;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[POI_SYNC] Error: {ex}");
                return false;
            }
        }

        private static string GetString(JsonElement item, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (item.TryGetProperty(key, out var value))
                {
                    if (value.ValueKind == JsonValueKind.String)
                        return value.GetString();

                    if (value.ValueKind == JsonValueKind.Number || value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
                        return value.ToString();
                }
            }

            return null;
        }

        private static double GetDouble(JsonElement item, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (!item.TryGetProperty(key, out var value))
                    continue;

                if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
                    return number;

                if (value.ValueKind == JsonValueKind.String)
                {
                    var text = value.GetString()?.Trim();
                    if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                        return parsed;

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        text = text.Replace(',', '.');
                        if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out parsed))
                            return parsed;
                    }
                }
            }

            return 0d;
        }

        private class ApiPoiDto
        {
            [JsonPropertyName("id")]
            public int Id { get; set; }

            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("description")]
            public string Description { get; set; }

            [JsonPropertyName("latitude")]
            public double Latitude { get; set; }

            [JsonPropertyName("longitude")]
            public double Longitude { get; set; }

            [JsonPropertyName("audio_url")]
            public string AudioUrl { get; set; }

            [JsonPropertyName("image_url")]
            public string ImageUrl { get; set; }
        }
    }
}
