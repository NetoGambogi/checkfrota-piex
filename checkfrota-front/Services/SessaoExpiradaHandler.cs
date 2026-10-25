using System.Net;

namespace checkfrota_front.Services;

/// <summary>
/// Token recusado pela API (expirou por inatividade ou foi revogado) em qualquer tela: encerra a
/// sessão e leva para o login, em vez de cada tela mostrar um erro genérico.
/// </summary>
public class SessaoExpiradaHandler : DelegatingHandler
{
    // Rotas que tratam o 401 por conta própria: a restauração da sessão e o próprio "Sair".
    private static readonly string[] Ignoradas = ["me", "logout", "dispositivos"];

    // Várias requisições podem voltar 401 ao mesmo tempo; só a primeira leva para o login.
    private static int _disparado;

    /// <summary>Chamado ao entrar de novo, para o próximo 401 voltar a levar ao login.</summary>
    public static void Rearmar() => Interlocked.Exchange(ref _disparado, 0);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var resposta = await base.SendAsync(request, cancellationToken);

        if (resposta.StatusCode == HttpStatusCode.Unauthorized
            && request.Headers.Authorization is not null
            && !Ignoradas.Contains(request.RequestUri?.AbsolutePath.Split("/api/").LastOrDefault()?.TrimEnd('/'))
            && Interlocked.Exchange(ref _disparado, 1) == 0)
        {
            ApiAuthService.EncerrarSessaoExpirada();
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (Shell.Current is null) return;

                await Shell.Current.GoToAsync("//login");
                await Shell.Current.DisplayAlertAsync(
                    "Sessão expirada",
                    "Faça login novamente para continuar. Registros salvos no aparelho serão enviados depois que você entrar.",
                    "OK");
            });
        }

        return resposta;
    }
}
