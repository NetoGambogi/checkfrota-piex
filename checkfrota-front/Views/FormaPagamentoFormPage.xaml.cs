using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class FormaPagamentoFormPage : ContentPage
{
    private readonly FormaPagamentoFormViewModel _viewModel;

    public FormaPagamentoFormPage(FormaPagamentoFormViewModel viewModel)
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
