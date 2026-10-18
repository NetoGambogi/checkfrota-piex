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
