using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class HodometroPage : ContentPage
{
    public HodometroPage(HodometroViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}