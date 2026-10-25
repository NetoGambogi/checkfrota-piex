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

    /// <summary>Texto do <c>LoadingOverlay</c> enquanto a tela está ocupada, ex: "Salvando…".</summary>
    [ObservableProperty]
    private string busyMessage = "Carregando…";

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
