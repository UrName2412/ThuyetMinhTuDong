// File: Services/DebugLogService.cs

namespace ThuyetMinhTuDong.Services
{
    public static class DebugLogService
    {
        public static void Log(string message)
        {
            System.Diagnostics.Debug.WriteLine($"{DateTime.Now:HH:mm:ss} - {message}");
        }
    }
}