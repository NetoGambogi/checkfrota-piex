using checkfrota_front.Views;

namespace checkfrota_front
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute(nameof(PerfilPage), typeof(PerfilPage));
            Routing.RegisterRoute(nameof(NotificacoesPage), typeof(NotificacoesPage));
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
            Routing.RegisterRoute(nameof(ViagemManagementPage), typeof(ViagemManagementPage));
            Routing.RegisterRoute(nameof(ViagemFormPage), typeof(ViagemFormPage));
            Routing.RegisterRoute(nameof(AbastecimentoManagementPage), typeof(AbastecimentoManagementPage));
            Routing.RegisterRoute(nameof(AbastecimentoFormPage), typeof(AbastecimentoFormPage));
            Routing.RegisterRoute(nameof(MotoristaViagemFormPage), typeof(MotoristaViagemFormPage));
            Routing.RegisterRoute(nameof(ConcluirViagemPage), typeof(ConcluirViagemPage));
            Routing.RegisterRoute(nameof(MeusAbastecimentosPage), typeof(MeusAbastecimentosPage));
            Routing.RegisterRoute(nameof(MotoristaAbastecimentoFormPage), typeof(MotoristaAbastecimentoFormPage));
            Routing.RegisterRoute(nameof(MotoristaOcorrenciaFormPage), typeof(MotoristaOcorrenciaFormPage));
            Routing.RegisterRoute(nameof(MinhasOcorrenciasPage), typeof(MinhasOcorrenciasPage));
            Routing.RegisterRoute(nameof(OcorrenciaManagementPage), typeof(OcorrenciaManagementPage));
            Routing.RegisterRoute(nameof(OcorrenciaDetalhePage), typeof(OcorrenciaDetalhePage));
        }
    }
}
