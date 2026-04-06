using SQLite;
using System.Text.Json.Serialization;

namespace ThuyetMinhTuDong.Models
{
    public class PointOfInterest
    {
        [PrimaryKey, AutoIncrement]
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

        // Soft delete fields
        [JsonPropertyName("is_deleted")]
        public bool IsDeleted { get; set; } = false;
        
        [JsonPropertyName("deleted_at")]
        public DateTime? DeletedAt { get; set; }
        
        [JsonPropertyName("deleted_by")]
        public string DeletedBy { get; set; }
    }
}
