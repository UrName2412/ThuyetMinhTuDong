using SQLite;

namespace ThuyetMinhTuDong.Models
{
    public class PointOfInterest
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; }
        public string Description { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string AudioUrl { get; set; }
    }
}
