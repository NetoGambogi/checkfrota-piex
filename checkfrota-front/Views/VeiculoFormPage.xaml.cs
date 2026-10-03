using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class VeiculoFormPage : ContentPage
{
    private readonly VeiculoFormViewModel _viewModel;

    public VeiculoFormPage(VeiculoFormViewModel viewModel)
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
