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
        }

        private async Task InitAsync()
        {
            if (_database != null)
                return;

            _database = new SQLiteAsyncConnection(_dbPath);
            await _database.CreateTableAsync<PointOfInterest>();
        }

        public async Task<List<PointOfInterest>> GetPOIsAsync()
        {
            await InitAsync();
            return await _database.Table<PointOfInterest>().ToListAsync();
        }

        public async Task<int> SavePOIAsync(PointOfInterest poi)
        {
            await InitAsync();
            if (poi.Id != 0)
                return await _database.UpdateAsync(poi);
            else
                return await _database.InsertAsync(poi);
        }

        public async Task<int> DeletePOIAsync(PointOfInterest poi)
        {
            await InitAsync();
            return await _database.DeleteAsync(poi);
        }
    }
}
