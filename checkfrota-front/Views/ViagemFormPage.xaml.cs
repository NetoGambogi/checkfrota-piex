using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class ViagemFormPage : ContentPage
{
    private readonly ViagemFormViewModel _viewModel;

    public ViagemFormPage(ViagemFormViewModel viewModel)
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
