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

    public ApiAuthService(HttpClient http)
    {
        _http = http;
        _http.BaseAddress = new Uri(BaseUrl);
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
        await SecureStorage.SetAsync("auth_token", login.Token);
        await SecureStorage.SetAsync("user_role", login.User.Role);
        await SecureStorage.SetAsync("user_name", login.User.Name);
        await SecureStorage.SetAsync("user_avatar", login.User.Avatar ?? string.Empty);
        await SecureStorage.SetAsync("user_created_at", login.User.CreatedAt ?? string.Empty);
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
            SecureStorage.Remove("auth_token");
            SecureStorage.Remove("user_role");
            SecureStorage.Remove("user_name");
            SecureStorage.Remove("user_avatar");
            SecureStorage.Remove("user_created_at");
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