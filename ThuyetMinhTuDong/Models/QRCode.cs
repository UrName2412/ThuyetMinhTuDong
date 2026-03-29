using SQLite;

namespace ThuyetMinhTuDong.Models
{
    public class QRCode
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string QRValue { get; set; }

        public int PoiId { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public string AudioUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
