using checkfrota_front.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace checkfrota_front.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IGoogleAuthService _googleAuth;
    private readonly ApiAuthService _apiAuth;
    private readonly NotificacaoService _notificacoes;
    private readonly SincronizacaoService _sincronizacao;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string? errorMessage;

    public LoginViewModel(IGoogleAuthService googleAuth, ApiAuthService apiAuth, NotificacaoService notificacoes, SincronizacaoService sincronizacao)
    {
        _googleAuth = googleAuth;
        _apiAuth = apiAuth;
        _notificacoes = notificacoes;
        _sincronizacao = sincronizacao;
    }

    /// <summary>Ao abrir o app, entra direto com a sessão salva (até 7 dias sem uso).</summary>
    [RelayCommand]
    private async Task AppearingAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;

            var role = await _apiAuth.RestaurarSessaoAsync();
            if (role is not null)
            {
                await EntrarAsync(role);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var signInResult = await _googleAuth.SignInAsync();
            if (signInResult is null)
            {
                ErrorMessage = "Login cancelado.";
                return;
            }

            var login = await _apiAuth.LoginWithGoogleAsync(signInResult);
            if (login is null)
            {
                ErrorMessage = "Não foi possível autenticar. Tente novamente.";
                return;
            }

            await EntrarAsync(login.User.Role);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado ao fazer login.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task EntrarAsync(string role)
    {
        // Roteamento por role
        var route = role switch
        {
            "admin" => "//admin",
            "motorista" => "//motorista",
            "frota" => "//frota",
            "financeiro" => "//financeiro",
            "pendente" => "//pendente",
            _ => throw new InvalidOperationException($"Role desconhecida: {role}")
        };

        await Shell.Current.GoToAsync(route);

        // Em segundo plano para não segurar a entrada no app (pode abrir o pedido de permissão).
        _ = _notificacoes.RegistrarDispositivoAsync();

        // Registros feitos sem rede enquanto a sessão estava expirada.
        _sincronizacao.Disparar();

        // Se o app foi aberto pelo toque numa notificação, leva para a tela dela.
        await _notificacoes.AbrirToquePendenteAsync();
    }
}