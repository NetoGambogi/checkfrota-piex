using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class FinanciamentoFormPage : ContentPage
{
    private readonly FinanciamentoFormViewModel _viewModel;

    public FinanciamentoFormPage(FinanciamentoFormViewModel viewModel)
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
