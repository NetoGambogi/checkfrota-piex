using checkfrota_front.Models;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace checkfrota_front.Services;

public class ApiAuthService
{
    private readonly HttpClient _http;

    // 10.0.2.2 é o alias do host no emulador Android para o "php artisan serve" local (porta 8000).
    // Em dispositivo físico, trocar pelo IP da máquina na rede local e usar HTTPS em produção.
    // Barra final obrigatória: combinada com caminhos relativos SEM "/" inicial abaixo,
    // isso garante que o HttpClient acrescente ao invés de substituir o "/api" do BaseAddress.
#if ANDROID
    private const string BaseUrl = "http://192.168.3.4:8000/api/";
#else
    private const string BaseUrl = "http://localhost:8000/api/";
#endif

    public ApiAuthService(HttpClient http)
    {
        _http = http;
        _http.BaseAddress = new Uri(BaseUrl);
    }

    public async Task<LoginResponse?> LoginWithGoogleAsync(string idToken)
    {
        var response = await _http.PostAsJsonAsync("auth/google", new { id_token = idToken });

        if (!response.IsSuccessStatusCode)
            return null; // token inválido, usuário rejeitado, etc.

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
    }

    public async Task<string?> GetTokenAsync() => await SecureStorage.GetAsync("auth_token");
    public async Task<string?> GetRoleAsync() => await SecureStorage.GetAsync("user_role");

    public void Logout()
    {
        SecureStorage.Remove("auth_token");
        SecureStorage.Remove("user_role");
        SecureStorage.Remove("user_name");
    }

    // Usar isso pra configurar qualquer HttpClient que precise chamar rotas autenticadas
    public async Task<HttpClient> GetAuthenticatedClientAsync()
    {
        var token = await GetTokenAsync();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _http;
    }
}