using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class RotaManagementPage : ContentPage
{
    private readonly RotaManagementViewModel _viewModel;

    public RotaManagementPage(RotaManagementViewModel viewModel)
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
