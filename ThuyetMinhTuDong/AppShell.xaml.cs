namespace ThuyetMinhTuDong
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute("languagesearch", typeof(LanguageSearchPage));
        }
    }
}
