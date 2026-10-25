using checkfrota_front.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace checkfrota_front.ViewModels;

public partial class PerfilViewModel : ObservableObject
{
    private readonly ApiAuthService _apiAuth;
    private readonly FilaOffline _fila;

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

    [ObservableProperty]
    private bool isBusy;

    public string AppVersion => $"Versão {AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})";

    public PerfilViewModel(ApiAuthService apiAuth, FilaOffline fila)
    {
        _apiAuth = apiAuth;
        _fila = fila;
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
        // Sair apaga o que o motorista registrou sem rede e ainda não foi enviado.
        var pendentes = (await _fila.ListarAsync()).Count;
        if (pendentes > 0)
        {
            var sair = await Shell.Current.DisplayAlertAsync(
                "Registros não enviados",
                pendentes == 1
                    ? "Há 1 registro salvo no aparelho que ainda não foi enviado. Se sair agora, ele será perdido."
                    : $"Há {pendentes} registros salvos no aparelho que ainda não foram enviados. Se sair agora, eles serão perdidos.",
                "Sair mesmo assim",
                "Cancelar");

            if (!sair) return;
        }

        if (IsBusy) return;

        try
        {
            IsBusy = true;
            await _apiAuth.LogoutAsync();
            await Shell.Current.GoToAsync("//login");
        }
        finally
        {
            IsBusy = false;
        }
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
