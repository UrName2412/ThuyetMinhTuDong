using ThuyetMinhTuDong.Data;
using ThuyetMinhTuDong.Models;

namespace ThuyetMinhTuDong.Services
{
    public static class QRCodeService
    {
        /// <summary>
        /// Initialize sample QR codes for testing
        /// </summary>
        public static async Task InitializeSampleQRCodesAsync(LocalDatabase database)
        {
            try
            {
                var existingQRCodes = await database.GetQRCodesAsync();
                
                // Only add sample data if no QR codes exist
                if (existingQRCodes == null || existingQRCodes.Count == 0)
                {
                    var sampleQRCodes = new List<QRCode>
                    {
                        new QRCode
                        {
                            QRValue = "STORE001",
                            Name = "Bún bò Huế Cô Ba",
                            Description = "Quán bún bò nổi tiếng hơn 30 năm tại phố Nguyễn Công Trứ. Nước dùng được ninh từ xương heo và sả, tạo nên hương vị đậm đà đặc trưng miền Trung",
                            Latitude = 16.4696,
                            Longitude = 107.5909,
                            AudioUrl = "https://example.com/bunbo.mp3"
                        },
                        new QRCode
                        {
                            QRValue = "STORE002",
                            Name = "Đại Nội Huế",
                            Description = "Hoàng thành lịch sử - Di sản văn hóa thế giới. Tìm hiểu về kiến trúc và lịch sử của triều đại Nguyễn",
                            Latitude = 16.4712,
                            Longitude = 107.5892,
                            AudioUrl = "https://example.com/dainoi.mp3"
                        },
                        new QRCode
                        {
                            QRValue = "STORE003",
                            Name = "Cafe Muối",
                            Description = "Đặc sản đồ uống nổi tiếng - Nước chanh đường phèn cổ truyền. Là một trong những quán cafe độc đáo nhất Huế",
                            Latitude = 16.4680,
                            Longitude = 107.5925,
                            AudioUrl = "https://example.com/cafe.mp3"
                        }
                    };

                    foreach (var qrCode in sampleQRCodes)
                    {
                        await database.SaveQRCodeAsync(qrCode);
                    }

                    System.Diagnostics.Debug.WriteLine("Sample QR codes initialized successfully");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing sample QR codes: {ex.Message}");
            }
        }

        /// <summary>
        /// Add a new QR code to the database
        /// </summary>
        public static async Task<bool> AddQRCodeAsync(LocalDatabase database, string qrValue, string name, string description, double latitude, double longitude, string audioUrl = "")
        {
            try
            {
                var qrCode = new QRCode
                {
                    QRValue = qrValue,
                    Name = name,
                    Description = description,
                    Latitude = latitude,
                    Longitude = longitude,
                    AudioUrl = audioUrl
                };

                await database.SaveQRCodeAsync(qrCode);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding QR code: {ex.Message}");
                return false;
            }
        }
    }
}
