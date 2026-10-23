using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class MinhasOcorrenciasPage : ContentPage
{
    private readonly MinhasOcorrenciasViewModel _viewModel;

    public MinhasOcorrenciasPage(MinhasOcorrenciasViewModel viewModel)
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
