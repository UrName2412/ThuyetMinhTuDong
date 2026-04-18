using System.Text;
using System.Text.Json;

namespace ThuyetMinhTuDong.Services
{
    public class UserService
    {
        private const string SupabaseUrl = "https://vkicutmxykziwygemslh.supabase.co";
        private const string SupabaseAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InZraWN1dG14eWt6aXd5Z2Vtc2xoIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NzU0MTc1NDAsImV4cCI6MjA5MDk5MzU0MH0.SVNFu7wpI-TTLRXDvAOX_KPRXIvX7TEQapi0DjNX2z0";

        private static readonly HttpClient _httpClient = new HttpClient();

        public async Task RegisterUserAsync()
        {
            try
            {
                // Sinh hoặc lấy deviceId
                var deviceId = Preferences.Default.Get("device_id", (string)null);
                if (string.IsNullOrWhiteSpace(deviceId))
                {
                    deviceId = Guid.NewGuid().ToString();
                    Preferences.Default.Set("device_id", deviceId);
                }

                // Gửi lên API với upsert (on_conflict=device_id)
                var url = $"{SupabaseUrl}/rest/v1/users?on_conflict=device_id";
                var payload = new { device_id = deviceId };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = content
                };
                request.Headers.Add("apikey", SupabaseAnonKey);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", SupabaseAnonKey);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"[USER] Registered user: {deviceId}");
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[USER] Register failed: {response.StatusCode} - {error}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[USER] Register error: {ex.Message}");
            }
        }
    }
}