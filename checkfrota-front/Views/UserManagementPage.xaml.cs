using checkfrota_front.ViewModels;

namespace checkfrota_front.Views;

public partial class UserManagementPage : ContentPage
{
    private readonly UserManagementViewModel _viewModel;

    public UserManagementPage(UserManagementViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.AppearingCommand.Execute(null);
    }

    private void OnRolePickerChanged(object? sender, EventArgs e)
    {
        if (sender is Picker { BindingContext: UserRowViewModel row })
        {
            _viewModel.UpdateRoleCommand.Execute(row);
        }
    }
}
