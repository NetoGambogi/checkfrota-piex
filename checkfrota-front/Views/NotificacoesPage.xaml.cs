using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class NotificacoesPage : ContentPage
{
    private readonly NotificacoesViewModel _viewModel;

    public NotificacoesPage(NotificacoesViewModel viewModel)
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
c7f7c1cc897c4b2d312e6dfdd41124a8a0d89b7f
