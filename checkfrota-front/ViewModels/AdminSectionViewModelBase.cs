using checkfrota_front.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace checkfrota_front.ViewModels;

public abstract partial class AdminSectionViewModelBase : ObservableObject
{
    private readonly ApiAuthService _apiAuth;

    [ObservableProperty]
    private string? avatarUrl;

    [ObservableProperty]
    private bool hasAvatar;

    protected AdminSectionViewModelBase(ApiAuthService apiAuth)
    {
        _apiAuth = apiAuth;
    }

    protected async Task LoadAvatarAsync()
    {
        AvatarUrl = await _apiAuth.GetAvatarAsync();
        HasAvatar = !string.IsNullOrWhiteSpace(AvatarUrl);
    }
}
