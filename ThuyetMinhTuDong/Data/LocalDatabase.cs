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
            await _database.CreateTableAsync<QRCode>();
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

        public async Task<List<QRCode>> GetQRCodesAsync()
        {
            await InitAsync();
            return await _database.Table<QRCode>().ToListAsync();
        }

        public async Task<QRCode> GetQRCodeByValueAsync(string qrValue)
        {
            await InitAsync();
            return await _database.Table<QRCode>()
                .FirstOrDefaultAsync(q => q.QRValue == qrValue);
        }

        public async Task<int> SaveQRCodeAsync(QRCode qrCode)
        {
            await InitAsync();
            if (qrCode.Id != 0)
                return await _database.UpdateAsync(qrCode);
            else
                return await _database.InsertAsync(qrCode);
        }

        public async Task<int> DeleteQRCodeAsync(QRCode qrCode)
        {
            await InitAsync();
            return await _database.DeleteAsync(qrCode);
        }
    }
}
