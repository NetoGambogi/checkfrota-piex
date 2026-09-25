using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class CategoriaFinanceiraFormPage : ContentPage
{
    private readonly CategoriaFinanceiraFormViewModel _viewModel;

    public CategoriaFinanceiraFormPage(CategoriaFinanceiraFormViewModel viewModel)
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
