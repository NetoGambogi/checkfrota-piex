using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class ManutencaoManagementPage : ContentPage
{
    private readonly ManutencaoManagementViewModel _viewModel;

    public ManutencaoManagementPage(ManutencaoManagementViewModel viewModel)
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
