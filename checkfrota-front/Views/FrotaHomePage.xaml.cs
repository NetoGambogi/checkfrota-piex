using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class FrotaHomePage : ContentPage
{
    private readonly FrotaHomeViewModel _viewModel;

    public FrotaHomePage(FrotaHomeViewModel viewModel)
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
