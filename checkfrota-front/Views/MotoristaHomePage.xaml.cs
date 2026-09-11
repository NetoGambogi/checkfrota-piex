using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class MotoristaHomePage : ContentPage
{
    private readonly MotoristaHomeViewModel _viewModel;

    public MotoristaHomePage(MotoristaHomeViewModel viewModel)
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
