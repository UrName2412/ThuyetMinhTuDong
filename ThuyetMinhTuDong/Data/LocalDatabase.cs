using SQLite;
using ThuyetMinhTuDong.Models;

namespace ThuyetMinhTuDong.Data
{
    public class LocalDatabase
    {
        private SQLiteAsyncConnection _database;
        private readonly string _dbPath;

        public LocalDatabase(string dbPath)
        {
            _dbPath = dbPath;
            System.Diagnostics.Debug.WriteLine($"ĐƯỜNG DẪN DATABASE: {_dbPath}");
        }

        private async Task InitAsync()
        {
            if (_database != null)
                return;

            _database = new SQLiteAsyncConnection(_dbPath);
            await _database.CreateTableAsync<PointOfInterest>();
            
            // Đảm bảo cột MapLink được tạo (trong trường hợp SQLiteNet không tự update schema cũ)
            try
            {
                await _database.ExecuteAsync("ALTER TABLE PointOfInterest ADD COLUMN MapLink TEXT");
                System.Diagnostics.Debug.WriteLine("Migration: Đã thêm cột MapLink.");
            }
            catch (SQLite.SQLiteException ex) when (ex.Message.Contains("duplicate column name"))
            {
                // Bỏ qua nếu cột đã tồn tại
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Migration error: {ex.Message}");
            }

            await _database.CreateTableAsync<TranslationCache>();
            await _database.CreateTableAsync<SyncState>();
        }

        // ===== POI Methods =====
        public async Task<List<PointOfInterest>> GetPOIsAsync()
        {
            await InitAsync();
            return await _database.Table<PointOfInterest>().ToListAsync();
        }

        public async Task<List<PointOfInterest>> GetActivePOIsAsync()
        {
            await InitAsync();
            return await _database.Table<PointOfInterest>()
                .Where(x => !x.IsDeleted)
                .ToListAsync();
        }

        public async Task<int> SavePOIAsync(PointOfInterest poi)
        {
            await InitAsync();
            var existingPoi = await _database.Table<PointOfInterest>().FirstOrDefaultAsync(x => x.Id == poi.Id);
            if (existingPoi != null)
            {
                // Ensure we don't accidentally un-delete a soft-deleted item via normal save
                if (existingPoi.IsDeleted && !poi.IsDeleted)
                {
                   poi.IsDeleted = true; // Keep local delete state unless explicitly restored
                }
                return await _database.UpdateAsync(poi);
            }
            else
            {
                return await _database.InsertAsync(poi);
            }
        }

        public async Task<int> SavePOIsBatchAsync(IEnumerable<PointOfInterest> pois)
        {
            await InitAsync();
            int count = 0;
            await _database.RunInTransactionAsync(tran =>
            {
                foreach (var poi in pois)
                {
                    var existingPoi = tran.Find<PointOfInterest>(poi.Id);
                    if (existingPoi != null)
                    {
                        if (existingPoi.IsDeleted && !poi.IsDeleted)
                        {
                            poi.IsDeleted = true;
                        }
                        count += tran.Update(poi);
                    }
                    else
                    {
                        count += tran.Insert(poi);
                    }
                }
            });
            return count;
        }

        public async Task<int> DeletePOIAsync(PointOfInterest poi)
        {
            await InitAsync();
            return await _database.DeleteAsync(poi);
        }

        public async Task SoftDeletePOIAsync(int poiId)
        {
            await InitAsync();
            var poi = await _database.FindAsync<PointOfInterest>(poiId);
            if (poi != null)
            {
                poi.IsDeleted = true;
                poi.DeletedAt = DateTime.Now;
                await _database.UpdateAsync(poi);
                
                // 🧹 Delete all translation cache for this POI
                await DeleteTranslationCacheForPOIAsync(poiId);
                
                System.Diagnostics.Debug.WriteLine($"[Sync] Soft deleted POI #{poiId}: {poi.Name}");
            }
        }

        public async Task RestorePOIAsync(int poiId)
        {
            await InitAsync();
            var poi = await _database.FindAsync<PointOfInterest>(poiId);
            if (poi != null)
            {
                poi.IsDeleted = false;
                poi.DeletedAt = null;
                poi.DeletedBy = null;
                await _database.UpdateAsync(poi);
                System.Diagnostics.Debug.WriteLine($"[Sync] Restored POI: {poi.Name}");
            }
        }

        public async Task<int> DeleteAllPOIsAsync()
        {
            await InitAsync();
            return await _database.DeleteAllAsync<PointOfInterest>();
        }

        // ===== Cleanup Methods =====
        /// <summary>
        /// Deletes POIs with empty or null names (data cleanup)
        /// Optimized to delete all at once using SQL
        /// </summary>
        public async Task<int> DeleteEmptyNamePOIsAsync()
        {
            await InitAsync();
            try
            {
                // Use SQL directly for efficiency
                int deletedCount = await _database.ExecuteAsync(
                    "DELETE FROM PointOfInterest WHERE Name IS NULL OR Name = ''");
                
                if (deletedCount > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[Cleanup] Deleted {deletedCount} POIs with empty names");
                }
                return deletedCount;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Cleanup] Error during cleanup: {ex.Message}");
                return 0;
            }
        }

        // ===== Sync State Methods =====
        public async Task<DateTime?> GetLastSyncTimeAsync(string key = "poi_last_sync")
        {
            await InitAsync();
            var syncState = await _database.Table<SyncState>()
                .FirstOrDefaultAsync(x => x.Key == key);
            return syncState?.LastSyncTime;
        }

        public async Task UpdateSyncTimeAsync(string key, DateTime syncTime)
        {
            await InitAsync();
            var syncState = new SyncState { Key = key, LastSyncTime = syncTime };
            await _database.InsertOrReplaceAsync(syncState);
        }

        // ===== Translation Cache Methods =====
        public async Task<TranslationCache> GetTranslationAsync(string sourceText, string targetLang)
        {
            await InitAsync();
            var cacheId = $"{sourceText}|{targetLang}";
            return await _database.Table<TranslationCache>()
                .FirstOrDefaultAsync(x => x.Id == cacheId);
        }

        public async Task SaveTranslationAsync(string sourceText, string targetLang, string translatedText, int? poiId = null)
        {
            await InitAsync();
            var cache = new TranslationCache
            {
                Id = $"{sourceText}|{targetLang}",
                POIId = poiId,  // ← Store POI ID for later cleanup
                SourceText = sourceText,
                TargetLanguage = targetLang,
                TranslatedText = translatedText,
                CreatedAt = DateTime.Now
            };

            await _database.InsertOrReplaceAsync(cache);
        }

        /// <summary>
        /// Delete all translation cache for a specific POI
        /// </summary>
        public async Task DeleteTranslationCacheForPOIAsync(int poiId)
        {
            await InitAsync();
            int deletedCount = await _database.ExecuteAsync(
                "DELETE FROM translation_cache WHERE POIId = ?", poiId);
            
            if (deletedCount > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[Cache] Deleted {deletedCount} translations for POI #{poiId}");
            }
        }

        public async Task CleanupOldCacheAsync(int daysOld = 30)
        {
            await InitAsync();
            var cutoffDate = DateTime.Now.AddDays(-daysOld);
            await _database.ExecuteAsync(
                "DELETE FROM translation_cache WHERE CreatedAt < ?", cutoffDate);
        }

        public async Task<int> GetCacheSizeAsync()
        {
            await InitAsync();
            return await _database.Table<TranslationCache>().CountAsync();
        }
    }

    [Table("sync_state")]
    public class SyncState
    {
        [PrimaryKey]
        public string Key { get; set; }
        public DateTime LastSyncTime { get; set; }
    }

    [Table("translation_cache")]
    public class TranslationCache
    {
        [PrimaryKey]
        public string Id { get; set; }
        
        public int? POIId { get; set; }  // ← Link với POI để xóa khi POI bị xóa
        public string SourceText { get; set; }
        public string TargetLanguage { get; set; }
        public string TranslatedText { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
