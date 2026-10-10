using checkfrota_front.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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
    private Task AbrirViagensAsync() => EmBreveAsync("Minhas Viagens");

    [RelayCommand]
    private Task AbrirHodometroAsync() => EmBreveAsync("Registrar Hodômetro");

    [RelayCommand]
    private Task AbrirPerfilAsync() => EmBreveAsync("Meu Perfil");

    private static Task EmBreveAsync(string tela) =>
        Shell.Current.DisplayAlert(tela, "Esta tela ainda será implementada.", "OK");
    
    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _apiAuth.LogoutAsync();
        await Shell.Current.GoToAsync("//login");
    }
}
