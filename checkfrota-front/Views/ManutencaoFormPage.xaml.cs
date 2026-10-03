using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class ManutencaoFormPage : ContentPage
{
    private readonly ManutencaoFormViewModel _viewModel;

    public ManutencaoFormPage(ManutencaoFormViewModel viewModel)
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
