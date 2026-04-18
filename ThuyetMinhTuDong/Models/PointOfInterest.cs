using SQLite;
using System.Text.Json.Serialization;

namespace ThuyetMinhTuDong.Models
{
    public class PointOfInterest
    {
        [PrimaryKey]  // Removed AutoIncrement to use server-provided Id directly
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }

        [JsonPropertyName("radius")]
        public int? Radius { get; set; }

        [JsonPropertyName("classification")]
        public string Classification { get; set; } = string.Empty;

        [JsonPropertyName("minor_category")]
        public string MinorCategory { get; set; } = string.Empty;

        [JsonPropertyName("audio_url")]
        public string AudioUrl { get; set; } = string.Empty;

        [JsonPropertyName("map_link")]
        public string MapLink { get; set; } = string.Empty;

        // Soft delete fields
        [JsonPropertyName("is_deleted")]
        public bool IsDeleted { get; set; } = false;

        [JsonPropertyName("deleted_at")]
        public DateTime? DeletedAt { get; set; }

        [JsonPropertyName("deleted_by")]
        public string? DeletedBy { get; set; }
    }
}
