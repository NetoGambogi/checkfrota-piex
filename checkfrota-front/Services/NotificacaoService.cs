using checkfrota_front.Models;
using checkfrota_front.ViewModels;
using checkfrota_front.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Net.Http.Json;

namespace checkfrota_front.Services;

// Singleton: o contador de não lidas é o mesmo no sino de todas as telas. Também registra o
// aparelho para receber push e abre a tela certa quando o usuário toca numa notificação.
public partial class NotificacaoService : ObservableObject
{
    // Token do push guardado no aparelho, para remover o registro na API ao sair da conta.
    public const string PreferenciaTokenPush = "push_token";

    private readonly ApiAuthService _apiAuth;
    private readonly IPushNotificationService _push;

    // Toque recebido antes de haver sessão aberta (app fechado cai no login); abre depois do login.
    private (string Id, string? Destino)? _toquePendente;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PossuiNaoLidas))]
    [NotifyPropertyChangedFor(nameof(NaoLidasTexto))]
    private int naoLidas;

    public bool PossuiNaoLidas => NaoLidas > 0;
    public string NaoLidasTexto => NaoLidas > 99 ? "99+" : NaoLidas.ToString();

    public NotificacaoService(ApiAuthService apiAuth, IPushNotificationService push)
    {
        _apiAuth = apiAuth;
        _push = push;
    }

    public async Task<List<NotificacaoItem>> ListarAsync()
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.GetFromJsonAsync<NotificacaoListResponse>("notificacoes");
        DefinirNaoLidas(response?.NaoLidas ?? 0);
        return response?.Notificacoes ?? new List<NotificacaoItem>();
    }

    public async Task AtualizarContadorAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(await _apiAuth.GetTokenAsync()))
                return;

            await ListarAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    public async Task MarcarComoLidaAsync(string id)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.PatchAsync($"notificacoes/{Uri.EscapeDataString(id)}/lida", null);
        await AtualizarContadorPelaRespostaAsync(response);
    }

    public async Task MarcarTodasComoLidasAsync()
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.PatchAsync("notificacoes/lidas", null);
        await AtualizarContadorPelaRespostaAsync(response);
    }

    // Chamado depois do login. Falhas são ignoradas: sem push, as notificações continuam no sino.
    public async Task RegistrarDispositivoAsync()
    {
        try
        {
            var token = await _push.ObterTokenAsync();
            if (!string.IsNullOrEmpty(token))
                await EnviarTokenAsync(token);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    // O Firebase pode trocar o token a qualquer momento; só reenvia se houver usuário logado.
    public async Task AtualizarTokenAsync(string token)
    {
        try
        {
            if (!string.IsNullOrEmpty(await _apiAuth.GetTokenAsync()))
                await EnviarTokenAsync(token);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    public void ReceberToque(string id, string? destino)
    {
        _toquePendente = (id, destino);

        if (PossuiSessaoAberta())
            MainThread.BeginInvokeOnMainThread(async () => await AbrirToquePendenteAsync());
    }

    public async Task AbrirToquePendenteAsync()
    {
        if (_toquePendente is not { } toque || !PossuiSessaoAberta())
            return;

        _toquePendente = null;
        var rota = await RotaDoDestinoAsync(toque.Destino) ?? nameof(NotificacoesPage);
        await MarcarComoLidaSemFalharAsync(toque.Id);
        await NavegarAsync(rota, toque.Destino);
    }

    // Abre a tela ligada à notificação; devolve false quando ela não tem tela própria.
    public async Task<bool> AbrirDestinoAsync(string? destino)
    {
        var rota = await RotaDoDestinoAsync(destino);
        if (rota is null)
            return false;

        await NavegarAsync(rota, destino);
        return true;
    }

    private async Task EnviarTokenAsync(string token)
    {
        Preferences.Set(PreferenciaTokenPush, token);

        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.PostAsJsonAsync("dispositivos", new { token, plataforma = _push.Plataforma });
        response.EnsureSuccessStatusCode();
    }

    private async Task MarcarComoLidaSemFalharAsync(string id)
    {
        try
        {
            await MarcarComoLidaAsync(id);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    // Respeita as telas que cada role pode abrir; sem permissão, fica na lista de notificações.
    private async Task<string?> RotaDoDestinoAsync(string? destino)
    {
        var role = await _apiAuth.GetRoleAsync();

        return destino switch
        {
            "movimentacoes" when role is "admin" or "financeiro" => nameof(MovimentacaoFinanceiraManagementPage),
            "viagens" when role is "admin" or "frota" => nameof(ViagemManagementPage),
            "usuarios" when role is "admin" => "//admin/usuarios",
            _ => null,
        };
    }

    private static Task NavegarAsync(string rota, string? destino)
    {
        if (destino != "movimentacoes")
            return Shell.Current.GoToAsync(rota);

        // Avisos financeiros falam de contas em aberto: abre os lançamentos pendentes, dos vencidos
        // no último ano até o próximo mês.
        var hoje = DateTime.Today;
        return Shell.Current.GoToAsync(rota, new Dictionary<string, object>
        {
            ["filtro"] = new FiltroLancamentos("todos", "pendente", hoje.AddYears(-1), hoje.AddMonths(1)),
        });
    }

    private static bool PossuiSessaoAberta()
    {
        var local = Shell.Current?.CurrentState?.Location?.OriginalString;
        return !string.IsNullOrEmpty(local)
            && !local.StartsWith("//login", StringComparison.OrdinalIgnoreCase)
            && !local.StartsWith("//pendente", StringComparison.OrdinalIgnoreCase);
    }

    private async Task AtualizarContadorPelaRespostaAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        var resultado = await response.Content.ReadFromJsonAsync<NaoLidasResponse>();
        DefinirNaoLidas(resultado?.NaoLidas ?? 0);
    }

    // O push chega numa thread do Firebase; o contador está ligado à UI.
    private void DefinirNaoLidas(int valor) =>
        MainThread.BeginInvokeOnMainThread(() => NaoLidas = valor);
}
