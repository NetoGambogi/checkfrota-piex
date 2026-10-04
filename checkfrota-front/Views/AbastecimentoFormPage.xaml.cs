using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class AbastecimentoFormPage : ContentPage
{
    private readonly AbastecimentoFormViewModel _viewModel;

    public AbastecimentoFormPage(AbastecimentoFormViewModel viewModel)
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
