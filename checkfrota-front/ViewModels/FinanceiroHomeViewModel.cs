using checkfrota_front.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace checkfrota_front.ViewModels;

public partial class FinanceiroHomeViewModel : AdminSectionViewModelBase
{
    private readonly ApiAuthService _apiAuth;

    [ObservableProperty]
    private string? userName;

    public FinanceiroHomeViewModel(ApiAuthService apiAuth) : base(apiAuth)
    {
        _apiAuth = apiAuth;
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        UserName = await SecureStorage.GetAsync("user_name");
        await LoadAvatarAsync();
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _apiAuth.LogoutAsync();
        await Shell.Current.GoToAsync("//login");
    }
}
