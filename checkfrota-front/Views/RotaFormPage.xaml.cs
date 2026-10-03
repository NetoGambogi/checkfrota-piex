using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class RotaFormPage : ContentPage
{
    private readonly RotaFormViewModel _viewModel;

    public RotaFormPage(RotaFormViewModel viewModel)
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
