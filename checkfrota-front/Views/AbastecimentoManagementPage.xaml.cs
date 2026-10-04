using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class AbastecimentoManagementPage : ContentPage
{
    private readonly AbastecimentoManagementViewModel _viewModel;

    public AbastecimentoManagementPage(AbastecimentoManagementViewModel viewModel)
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
