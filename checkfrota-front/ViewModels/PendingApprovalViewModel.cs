using checkfrota_front.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;

namespace checkfrota_front.ViewModels;

public partial class PendingApprovalViewModel : ObservableObject
{
    private readonly ApiAuthService _apiAuth;

    [ObservableProperty]
    private string requestedAtText = "--/--/---- --:--";

    [ObservableProperty]
    private string? avatarUrl;

    [ObservableProperty]
    private bool hasAvatar;

    public PendingApprovalViewModel(ApiAuthService apiAuth)
    {
        _apiAuth = apiAuth;
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        var raw = await _apiAuth.GetCreatedAtAsync();
        RequestedAtText = DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var createdAt)
            ? createdAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
            : "--/--/---- --:--";

        var avatar = await _apiAuth.GetAvatarAsync();
        HasAvatar = !string.IsNullOrWhiteSpace(avatar);
        AvatarUrl = avatar;
    }

    [RelayCommand]
    private async Task VoltarParaLoginAsync()
    {
        await _apiAuth.LogoutAsync();
        await Shell.Current.GoToAsync("//login");
    }
}
