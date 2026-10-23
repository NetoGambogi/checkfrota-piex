using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class MotoristaAbastecimentoFormPage : ContentPage
{
    private readonly MotoristaAbastecimentoFormViewModel _viewModel;

    public MotoristaAbastecimentoFormPage(MotoristaAbastecimentoFormViewModel viewModel)
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
