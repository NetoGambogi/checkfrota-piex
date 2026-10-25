using checkfrota_front.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;

namespace checkfrota_front.Services;

public class ApiAuthService
{
    private readonly HttpClient _http;

    // Barra final obrigatória: combinada com caminhos relativos SEM "/" inicial abaixo,
    // isso garante que o HttpClient acrescente ao invés de substituir o "/api" do BaseAddress.
    private const string ProducaoUrl = "https://checkfrota.netogambogi.cloud/api/";

    private static readonly string BaseUrl = ResolverBaseUrl();

    /// <summary>
    /// Em Debug (propriedade ApiLocal do csproj) usa o "php artisan serve" do PC: localhost no Windows,
    /// 10.0.2.2 (alias do host) no emulador Android e o DevApiHost (IP do PC na rede) em celular físico.
    /// No celular, o servidor precisa ouvir na rede: "php artisan serve --host=0.0.0.0".
    /// </summary>
    private static string ResolverBaseUrl()
    {
        var config = typeof(ApiAuthService).Assembly
            .GetCustomAttributes(typeof(AssemblyMetadataAttribute), false)
            .Cast<AssemblyMetadataAttribute>()
            .ToDictionary(a => a.Key, a => a.Value);

        if (config.GetValueOrDefault("ApiLocal") != "true")
            return ProducaoUrl;

#if ANDROID
        var host = DeviceInfo.Current.DeviceType == DeviceType.Virtual
            ? "10.0.2.2"
            : config.GetValueOrDefault("DevApiHost") ?? "localhost";
#else
        var host = "localhost";
#endif
        return $"http://{host}:8000/api/";
    }

    /// <summary>
    /// Dias sem abrir o app até a sessão expirar; o mesmo prazo da API (RenovarExpiracaoToken), que
    /// renova o token a cada requisição.
    /// </summary>
    public const int DiasInatividade = 7;

    private const string PreferenciaUltimoUso = "sessao_ultimo_uso";

    private static readonly string[] ChavesSessao =
        ["auth_token", "user_role", "user_name", "user_avatar", "user_created_at"];

    private readonly CacheLocal _cache;
    private readonly FilaOffline _fila;

    public ApiAuthService(HttpClient http, CacheLocal cache, FilaOffline fila)
    {
        _http = http;
        _http.BaseAddress = new Uri(BaseUrl);
        _cache = cache;
        _fila = fila;
    }

    public async Task<LoginResponse?> LoginWithGoogleAsync(GoogleSignInResult signInResult)
    {
        var response = signInResult switch
        {
            GoogleIdTokenResult r => await _http.PostAsJsonAsync("auth/google", new { id_token = r.IdToken }),
            GoogleAuthCodeResult r => await _http.PostAsJsonAsync("auth/google/desktop", new
            {
                code = r.Code,
                code_verifier = r.CodeVerifier,
                redirect_uri = r.RedirectUri,
            }),
            _ => throw new NotSupportedException($"Tipo de resultado de login não suportado: {signInResult.GetType().Name}")
        };

        if (!response.IsSuccessStatusCode)
            return null; // token/code inválido, usuário rejeitado, etc.

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        if (result is not null)
            await SaveSessionAsync(result);

        return result;
    }

    public async Task SaveSessionAsync(LoginResponse login)
    {
        // Cache e fila offline ficam guardados quando a sessão expira, para o mesmo usuário enviar
        // o que registrou sem rede ao entrar de novo. Se quem entra é outra pessoa, são descartados.
        var usuarioAnterior = await SecureStorage.GetAsync("user_id");
        if (usuarioAnterior is not null && usuarioAnterior != login.User.Id.ToString())
        {
            _cache.Limpar();
            await _fila.LimparAsync();
        }

        await SecureStorage.SetAsync("user_id", login.User.Id.ToString());
        await SecureStorage.SetAsync("auth_token", login.Token);
        await SecureStorage.SetAsync("user_role", login.User.Role);
        await SecureStorage.SetAsync("user_name", login.User.Name);
        await SecureStorage.SetAsync("user_avatar", login.User.Avatar ?? string.Empty);
        await SecureStorage.SetAsync("user_created_at", login.User.CreatedAt ?? string.Empty);

        SessaoExpiradaHandler.Rearmar();
        RegistrarUso();
    }

    /// <summary>Marca o app como usado agora: a sessão expira <see cref="DiasInatividade"/> dias depois disso.</summary>
    public static void RegistrarUso() => Preferences.Set(PreferenciaUltimoUso, DateTime.UtcNow);

    /// <summary>Passou do prazo de inatividade desde o último uso do app.</summary>
    public static bool InatividadeExcedida() =>
        DateTime.UtcNow - Preferences.Get(PreferenciaUltimoUso, DateTime.MinValue) > TimeSpan.FromDays(DiasInatividade);

    /// <summary>
    /// Sessão salva ao abrir o app: devolve a role para entrar direto, ou null se é preciso fazer
    /// login (sem sessão, inativo há mais de <see cref="DiasInatividade"/> dias ou token recusado
    /// pela API). Sem rede, confia na sessão salva para o motorista seguir usando o app offline.
    /// </summary>
    public async Task<string?> RestaurarSessaoAsync()
    {
        if (string.IsNullOrEmpty(await GetTokenAsync())) return null;

        if (InatividadeExcedida())
        {
            EncerrarSessaoExpirada();
            return null;
        }

        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
        {
            try
            {
                // Atualiza a role (ex: cadastro pendente aprovado enquanto o app estava fechado).
                using var tempoLimite = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var client = await GetAuthenticatedClientAsync();
                var resposta = await client.GetAsync("me", tempoLimite.Token);

                if (resposta.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    EncerrarSessaoExpirada();
                    return null;
                }

                if (resposta.IsSuccessStatusCode && await resposta.Content.ReadFromJsonAsync<UserModel>() is { } usuario)
                {
                    await SecureStorage.SetAsync("user_role", usuario.Role);
                    await SecureStorage.SetAsync("user_name", usuario.Name);
                    await SecureStorage.SetAsync("user_avatar", usuario.Avatar ?? string.Empty);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // Servidor fora do ar ou sinal fraco: segue com os dados salvos.
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        RegistrarUso();
        return await GetRoleAsync();
    }

    /// <summary>
    /// Sessão expirada: apaga as credenciais, mas mantém o cache e a fila offline do usuário
    /// (ver <see cref="SaveSessionAsync"/>). O "Sair" explícito usa <see cref="LogoutAsync"/>.
    /// </summary>
    public static void EncerrarSessaoExpirada()
    {
        foreach (var chave in ChavesSessao)
            SecureStorage.Remove(chave);
    }

    public async Task<string?> GetTokenAsync() => await SecureStorage.GetAsync("auth_token");
    public async Task<string?> GetNameAsync() => await SecureStorage.GetAsync("user_name");
    public async Task<string?> GetRoleAsync() => await SecureStorage.GetAsync("user_role");
    public async Task<string?> GetAvatarAsync() => await SecureStorage.GetAsync("user_avatar");
    public async Task<string?> GetCreatedAtAsync() => await SecureStorage.GetAsync("user_created_at");

    public async Task LogoutAsync()
    {
        try
        {
            var token = await GetTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Para de mandar push da conta para este aparelho.
                var pushToken = Preferences.Get(NotificacaoService.PreferenciaTokenPush, string.Empty);
                if (!string.IsNullOrEmpty(pushToken))
                {
                    await _http.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "dispositivos")
                    {
                        Content = JsonContent.Create(new { token = pushToken }),
                    });
                }

                await _http.PostAsync("logout", null);
            }
        }
        catch
        {
            // Sem rede, token já expirado, etc. — segue limpando a sessão local mesmo assim.
        }
        finally
        {
            EncerrarSessaoExpirada();
            SecureStorage.Remove("user_id");

            // Dados offline são do usuário que saiu (quem chamou já confirmou o descarte das pendências).
            _cache.Limpar();
            await _fila.LimparAsync();
        }
    }

    // Usar isso pra configurar qualquer HttpClient que precise chamar rotas autenticadas
    public async Task<HttpClient> GetAuthenticatedClientAsync()
    {
        var token = await GetTokenAsync();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _http;
    }
}