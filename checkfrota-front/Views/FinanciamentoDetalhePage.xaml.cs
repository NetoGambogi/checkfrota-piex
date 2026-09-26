using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class FinanciamentoDetalhePage : ContentPage
{
    private readonly FinanciamentoDetalheViewModel _viewModel;

    public FinanciamentoDetalhePage(FinanciamentoDetalheViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.AppearingCommand.Execute(null);
    }
}
