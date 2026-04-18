using System.Text.Json.Serialization;

namespace ThuyetMinhTuDong.Models
{
    public class Image
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("poi_id")]
        public long PoiId { get; set; }

        [JsonPropertyName("image_url")]
        public string ImageUrl { get; set; } = string.Empty;
    }
}
