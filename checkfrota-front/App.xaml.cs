using checkfrota_front.Helpers;
using checkfrota_front.Resources.Styles;

namespace checkfrota_front
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            ApplyTheme(RequestedTheme);
            RequestedThemeChanged += (_, e) => ApplyTheme(e.RequestedTheme);
            PageAppearing += (_, page) =>
            {
                if (page is not Shell)
                    StatusBarHelper.Update(page);
            };
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }

        // Troca a paleta (LightTheme/DarkTheme) seguindo o tema do sistema. As telas usam
        // {DynamicResource ...}, então as cores se atualizam sem recriar as páginas.
        private void ApplyTheme(AppTheme theme)
        {
            var merged = Resources.MergedDictionaries;
            var current = merged.FirstOrDefault(d => d is LightTheme or DarkTheme);
            ResourceDictionary next = theme == AppTheme.Dark ? new DarkTheme() : new LightTheme();

            if (current?.GetType() == next.GetType())
                return;

            if (current is null)
            {
                merged.Add(next);
            }
            else
            {
                merged.Remove(current);
                merged.Add(next);
            }

            StatusBarHelper.Update(Shell.Current?.CurrentPage);
        }
    }
}
