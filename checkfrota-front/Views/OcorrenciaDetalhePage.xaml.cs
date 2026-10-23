using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class OcorrenciaDetalhePage : ContentPage
{
    private readonly OcorrenciaDetalheViewModel _viewModel;

    public OcorrenciaDetalhePage(OcorrenciaDetalheViewModel viewModel)
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
