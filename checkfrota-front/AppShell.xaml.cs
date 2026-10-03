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
            Routing.RegisterRoute(nameof(MovimentacaoFinanceiraManagementPage), typeof(MovimentacaoFinanceiraManagementPage));
            Routing.RegisterRoute(nameof(MovimentacaoFinanceiraFormPage), typeof(MovimentacaoFinanceiraFormPage));
            Routing.RegisterRoute(nameof(ComprovanteViewerPage), typeof(ComprovanteViewerPage));
            Routing.RegisterRoute(nameof(FinanciamentoManagementPage), typeof(FinanciamentoManagementPage));
            Routing.RegisterRoute(nameof(FinanciamentoFormPage), typeof(FinanciamentoFormPage));
            Routing.RegisterRoute(nameof(FinanciamentoDetalhePage), typeof(FinanciamentoDetalhePage));
            Routing.RegisterRoute(nameof(VeiculoManagementPage), typeof(VeiculoManagementPage));
            Routing.RegisterRoute(nameof(VeiculoFormPage), typeof(VeiculoFormPage));
            Routing.RegisterRoute(nameof(ManutencaoManagementPage), typeof(ManutencaoManagementPage));
            Routing.RegisterRoute(nameof(ManutencaoFormPage), typeof(ManutencaoFormPage));
            Routing.RegisterRoute(nameof(RotaManagementPage), typeof(RotaManagementPage));
            Routing.RegisterRoute(nameof(RotaFormPage), typeof(RotaFormPage));
        }
    }
}
