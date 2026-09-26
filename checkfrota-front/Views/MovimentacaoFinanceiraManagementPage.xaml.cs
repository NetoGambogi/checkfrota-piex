using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class MovimentacaoFinanceiraManagementPage : ContentPage
{
    private readonly MovimentacaoFinanceiraManagementViewModel _viewModel;

    public MovimentacaoFinanceiraManagementPage(MovimentacaoFinanceiraManagementViewModel viewModel)
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
