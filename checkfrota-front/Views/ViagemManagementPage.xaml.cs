using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class ViagemManagementPage : ContentPage
{
    private readonly ViagemManagementViewModel _viewModel;

    public ViagemManagementPage(ViagemManagementViewModel viewModel)
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
