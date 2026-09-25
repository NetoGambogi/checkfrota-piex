using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class FormaPagamentoManagementPage : ContentPage
{
    private readonly FormaPagamentoManagementViewModel _viewModel;

    public FormaPagamentoManagementPage(FormaPagamentoManagementViewModel viewModel)
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
