using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class MinhasViagensPage : ContentPage
{
    private readonly MinhasViagensViewModel _viewModel;

    public MinhasViagensPage(MinhasViagensViewModel viewModel)
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