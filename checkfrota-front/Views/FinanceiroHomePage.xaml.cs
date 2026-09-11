using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class FinanceiroHomePage : ContentPage
{
    private readonly FinanceiroHomeViewModel _viewModel;

    public FinanceiroHomePage(FinanceiroHomeViewModel viewModel)
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
