using ThuyetMinhTuDong.Models;

namespace ThuyetMinhTuDong.Repositories
{
    public interface IPoiRepository
    {
        Task<int> DeleteEmptyNamePOIsAsync();
        Task<DateTime?> GetLastSyncTimeAsync(string key = "poi_last_sync");
        Task<bool> SyncPOIsFromApiAsync(string apiUrl, string apiKey);
        Task CleanupSoftDeletedPOIsAsync(int daysOld = 90);
        Task EnsureDefaultPOIsAsync(Location userLocation);
        Task<List<PointOfInterest>> GetAllActivePOIsAsync(bool forceRefresh = false);
        Task<List<PointOfInterest>> GetNearbyActivePOIsAsync(Location userLocation, double radiusKm = 2, bool forceRefresh = false);
        Microsoft.Maui.Controls.Maps.Pin? CreateMapPin(PointOfInterest poi);

        // Lấy danh sách ảnh theo POI (fallback theo tên khi id local không khớp)
        Task<List<Models.Image>> GetImagesByPoiIdAsync(long poiId, string? poiName = null);
    }
}
