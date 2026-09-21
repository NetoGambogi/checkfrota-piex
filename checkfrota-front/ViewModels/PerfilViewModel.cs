using checkfrota_front.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace checkfrota_front.ViewModels;

public partial class PerfilViewModel : ObservableObject
{
    private readonly ApiAuthService _apiAuth;

    [ObservableProperty]
    private string? userName;

    [ObservableProperty]
    private string? roleDisplay;

    [ObservableProperty]
    private string? avatarUrl;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoAvatar))]
    private bool hasAvatar;

    [ObservableProperty]
    private string? initials;

    public bool NoAvatar => !HasAvatar;

    public string AppVersion => $"Versão {AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})";

    public PerfilViewModel(ApiAuthService apiAuth)
    {
        _apiAuth = apiAuth;
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        UserName = await _apiAuth.GetNameAsync();
        RoleDisplay = FormatRole(await _apiAuth.GetRoleAsync());

        AvatarUrl = await _apiAuth.GetAvatarAsync();
        HasAvatar = !string.IsNullOrWhiteSpace(AvatarUrl);
        Initials = BuildInitials(UserName);
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _apiAuth.LogoutAsync();
        await Shell.Current.GoToAsync("//login");
    }

    private static string FormatRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return "-";

        return char.ToUpperInvariant(role[0]) + role[1..];
    }

    private static string BuildInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "?";

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..1].ToUpperInvariant(),
            _ => (parts[0][..1] + parts[^1][..1]).ToUpperInvariant()
        };
    }
}
