using checkfrota_front.Views;

namespace checkfrota_front
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(PerfilPage), typeof(PerfilPage));
        }
    }
}
