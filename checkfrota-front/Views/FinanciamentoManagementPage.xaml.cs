using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class FinanciamentoManagementPage : ContentPage
{
    private readonly FinanciamentoManagementViewModel _viewModel;

    public FinanciamentoManagementPage(FinanciamentoManagementViewModel viewModel)
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
