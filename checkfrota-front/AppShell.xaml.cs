using checkfrota_front.Views;

namespace checkfrota_front
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(PerfilPage), typeof(PerfilPage));
            Routing.RegisterRoute(nameof(CategoriaFinanceiraManagementPage), typeof(CategoriaFinanceiraManagementPage));
            Routing.RegisterRoute(nameof(CategoriaFinanceiraFormPage), typeof(CategoriaFinanceiraFormPage));
            Routing.RegisterRoute(nameof(FormaPagamentoManagementPage), typeof(FormaPagamentoManagementPage));
            Routing.RegisterRoute(nameof(FormaPagamentoFormPage), typeof(FormaPagamentoFormPage));
        }
    }
}
