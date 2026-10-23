using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class ConcluirViagemPage : ContentPage
{
    private readonly ConcluirViagemViewModel _viewModel;

    public ConcluirViagemPage(ConcluirViagemViewModel viewModel)
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
