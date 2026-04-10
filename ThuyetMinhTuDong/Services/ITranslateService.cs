namespace ThuyetMinhTuDong.Services
{
    public interface ITranslateService
    {
        Task InitializeAsync();
        Task<string> TranslateTextAsync(string text, string targetLangCode);
    }
}
