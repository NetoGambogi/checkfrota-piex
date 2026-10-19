using checkfrota_front.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using checkfrota_front.Views;

namespace checkfrota_front.ViewModels;

public partial class MotoristaHomeViewModel : ObservableObject
{
    private readonly ApiAuthService _apiAuth;

    [ObservableProperty]
    private string? userName;

    [ObservableProperty]
    private string statusTexto = "ON-LINE";

    [ObservableProperty]
    private Color statusCor = Colors.Green;
    
    [ObservableProperty]
    private string rotaDescricao = "Varginha - MG";

    [ObservableProperty]
    private string entregaDescricao = "Entrega: 00000";

    [ObservableProperty]
    private string statusRota = "Status: ---";

    public MotoristaHomeViewModel(ApiAuthService apiAuth)
    {
        _apiAuth = apiAuth;
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        UserName = await SecureStorage.GetAsync("user_name");
        AtualizarStatusConexao();
    }

    private void AtualizarStatusConexao()
    {
        var online = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
        StatusTexto = online ? "ON-LINE" : "OFF-LINE";
        StatusCor = online ? Colors.Green : Colors.Orange;
    }

    [RelayCommand]
    private Task AbrirViagensAsync() => Shell.Current.GoToAsync("//motorista/viagens");

    [RelayCommand]
    private Task AbrirHodometroAsync() => Shell.Current.GoToAsync(nameof(HodometroPage));

    [RelayCommand]
    private Task AbrirNotificacoesAsync() => Shell.Current.GoToAsync("//motorista/notificacoes");

    [RelayCommand]
    private Task AbrirPerfilAsync() => Shell.Current.GoToAsync(nameof(PerfilPage));
    
    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _apiAuth.LogoutAsync();
        await Shell.Current.GoToAsync("//login");
    }
}
