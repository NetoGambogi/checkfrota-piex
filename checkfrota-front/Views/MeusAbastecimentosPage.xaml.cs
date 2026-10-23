using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class MeusAbastecimentosPage : ContentPage
{
    private readonly MeusAbastecimentosViewModel _viewModel;

    public MeusAbastecimentosPage(MeusAbastecimentosViewModel viewModel)
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
