using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class PendingApprovalPage : ContentPage
{
    private readonly PendingApprovalViewModel _viewModel;

    public PendingApprovalPage(PendingApprovalViewModel viewModel)
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
