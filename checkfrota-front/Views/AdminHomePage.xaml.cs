using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class AdminHomePage : ContentPage
{
    private readonly AdminHomeViewModel _viewModel;

    public AdminHomePage(AdminHomeViewModel viewModel)
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
