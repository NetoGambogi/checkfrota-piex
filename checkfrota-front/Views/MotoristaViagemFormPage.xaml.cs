using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class MotoristaViagemFormPage : ContentPage
{
    private readonly MotoristaViagemFormViewModel _viewModel;

    public MotoristaViagemFormPage(MotoristaViagemFormViewModel viewModel)
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
