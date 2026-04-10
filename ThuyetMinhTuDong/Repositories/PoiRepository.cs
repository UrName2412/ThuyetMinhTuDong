using ThuyetMinhTuDong.Data;
using ThuyetMinhTuDong.Models;
using ThuyetMinhTuDong.Services;

namespace ThuyetMinhTuDong.Repositories
{
    public class PoiRepository : IPoiRepository
    {
        private readonly LocalDatabase _database;
        private readonly PlaceService _placeService;

        private const string SupabaseImageApiUrl = "https://vkicutmxykziwygemslh.supabase.co/rest/v1/Image?select=*";
        private const string SupabasePoiApiUrl = "https://vkicutmxykziwygemslh.supabase.co/rest/v1/poi?select=id";
        private const string SupabaseAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InZraWN1dG14eWt6aXd5Z2Vtc2xoIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NzU0MTc1NDAsImV4cCI6MjA5MDk5MzU0MH0.SVNFu7wpI-TTLRXDvAOX_KPRXIvX7TEQapi0DjNX2z0";

        public PoiRepository(LocalDatabase database, PlaceService placeService)
        {
            _database = database;
            _placeService = placeService;
        }

        public Task<int> DeleteEmptyNamePOIsAsync()
            => _database.DeleteEmptyNamePOIsAsync();

        public Task<DateTime?> GetLastSyncTimeAsync(string key = "poi_last_sync")
            => _database.GetLastSyncTimeAsync(key);

        public Task<bool> SyncPOIsFromApiAsync(string apiUrl, string apiKey)
            => _placeService.SyncPOIsFromApiAsync(apiUrl, apiKey);

        public Task CleanupSoftDeletedPOIsAsync(int daysOld = 90)
            => _placeService.CleanupSoftDeletedPOIsAsync(daysOld);

        public Task EnsureDefaultPOIsAsync(Location userLocation)
            => _placeService.EnsureDefaultPOIsAsync(userLocation);

        public Task<List<PointOfInterest>> GetAllActivePOIsAsync(bool forceRefresh = false)
            => _placeService.GetAllActivePOIsAsync(forceRefresh);

        public async Task<List<PointOfInterest>> GetNearbyActivePOIsAsync(Location userLocation, double radiusKm = 2, bool forceRefresh = false)
        {
            if (userLocation == null || radiusKm <= 0)
                return new List<PointOfInterest>();

            var activePois = await _placeService.GetAllActivePOIsAsync(forceRefresh);

            return activePois
                .Where(poi =>
                    Location.CalculateDistance(
                        userLocation.Latitude,
                        userLocation.Longitude,
                        poi.Latitude,
                        poi.Longitude,
                        Microsoft.Maui.Devices.Sensors.DistanceUnits.Kilometers) <= radiusKm)
                .ToList();
        }

        public Microsoft.Maui.Controls.Maps.Pin? CreateMapPin(PointOfInterest poi)
            => _placeService.CreateMapPin(poi);

        // Lấy danh sách ảnh từ Supabase REST API theo poi_id
        public async Task<List<Models.Image>> GetImagesByPoiIdAsync(long poiId, string? poiName = null)
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("apikey", SupabaseAnonKey);
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {SupabaseAnonKey}");

                var images = await FetchImagesByPoiIdAsync(client, poiId);
                if (images.Count > 0)
                    return images;

                // Fallback: id local có thể lệch so với id trên Supabase,
                // thử tìm id theo tên POI rồi lấy ảnh lại.
                if (!string.IsNullOrWhiteSpace(poiName))
                {
                    var remotePoiIds = await FetchRemotePoiIdsByNameAsync(client, poiName);
                    foreach (var remotePoiId in remotePoiIds)
                    {
                        images = await FetchImagesByPoiIdAsync(client, remotePoiId);
                        if (images.Count > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Image Sync] Fallback success. LocalId={poiId}, RemoteId={remotePoiId}, Name={poiName}");
                            return images;
                        }
                    }
                }

                return new List<Models.Image>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Image Sync] Exception: {ex.Message}");
                return new List<Models.Image>();
            }
        }

        private static async Task<List<Models.Image>> FetchImagesByPoiIdAsync(HttpClient client, long poiId)
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            // Thử cả Image và image để tránh lỗi phân biệt hoa thường của table
            var url = $"{SupabaseImageApiUrl}&poi_id=eq.{poiId}";
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                url = url.Replace("/Image?", "/image?");
                response = await client.GetAsync(url);
            }

            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"[Image Sync] Error({poiId}): {response.StatusCode}");
                return new List<Models.Image>();
            }

            var json = await response.Content.ReadAsStringAsync();
            var images = System.Text.Json.JsonSerializer.Deserialize<List<Models.Image>>(json, options);
            return images ?? new List<Models.Image>();
        }

        private static async Task<List<long>> FetchRemotePoiIdsByNameAsync(HttpClient client, string poiName)
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var encodedName = Uri.EscapeDataString(poiName.Trim());
            var url = $"{SupabasePoiApiUrl}&name=eq.{encodedName}";
            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return new List<long>();

            var json = await response.Content.ReadAsStringAsync();
            var items = System.Text.Json.JsonSerializer.Deserialize<List<PoiIdDto>>(json, options) ?? new();
            return items.Select(x => x.Id).Distinct().ToList();
        }

        private class PoiIdDto
        {
            public long Id { get; set; }
        }
    }
}
