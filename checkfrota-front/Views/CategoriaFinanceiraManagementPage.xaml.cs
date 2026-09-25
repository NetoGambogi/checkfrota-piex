using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class CategoriaFinanceiraManagementPage : ContentPage
{
    private readonly CategoriaFinanceiraManagementViewModel _viewModel;

    public CategoriaFinanceiraManagementPage(CategoriaFinanceiraManagementViewModel viewModel)
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
