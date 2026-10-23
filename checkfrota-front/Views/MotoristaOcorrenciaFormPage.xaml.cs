using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class MotoristaOcorrenciaFormPage : ContentPage
{
    private readonly MotoristaOcorrenciaFormViewModel _viewModel;

    public MotoristaOcorrenciaFormPage(MotoristaOcorrenciaFormViewModel viewModel)
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
