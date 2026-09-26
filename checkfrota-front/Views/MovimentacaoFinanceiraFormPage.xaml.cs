using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class MovimentacaoFinanceiraFormPage : ContentPage
{
    private readonly MovimentacaoFinanceiraFormViewModel _viewModel;

    public MovimentacaoFinanceiraFormPage(MovimentacaoFinanceiraFormViewModel viewModel)
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
