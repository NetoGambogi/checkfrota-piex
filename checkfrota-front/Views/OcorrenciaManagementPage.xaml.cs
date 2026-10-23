using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class OcorrenciaManagementPage : ContentPage
{
    private readonly OcorrenciaManagementViewModel _viewModel;

    public OcorrenciaManagementPage(OcorrenciaManagementViewModel viewModel)
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
