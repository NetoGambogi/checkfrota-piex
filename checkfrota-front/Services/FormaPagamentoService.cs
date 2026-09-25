using checkfrota_front.Models;
using System.Net.Http.Json;

namespace checkfrota_front.Services;

public class FormaPagamentoService
{
    private readonly ApiAuthService _apiAuth;

    public FormaPagamentoService(ApiAuthService apiAuth)
    {
        _apiAuth = apiAuth;
    }

    public async Task<List<FormaPagamentoListItem>> GetFormasAsync(string status = "ativos")
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.GetFromJsonAsync<FormaPagamentoListResponse>($"formas-pagamento?status={Uri.EscapeDataString(status)}");
        return response?.Formas ?? new List<FormaPagamentoListItem>();
    }

    public async Task<FormaPagamentoListItem?> CreateAsync(string nome)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.PostAsJsonAsync("formas-pagamento", new { nome });
        if (!response.IsSuccessStatusCode) return null;

        var result = await response.Content.ReadFromJsonAsync<FormaPagamentoResponse>();
        return result?.Forma;
    }

    public async Task<FormaPagamentoListItem?> UpdateAsync(int id, string nome)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.PutAsJsonAsync($"formas-pagamento/{id}", new { nome });
        if (!response.IsSuccessStatusCode) return null;

        var result = await response.Content.ReadFromJsonAsync<FormaPagamentoResponse>();
        return result?.Forma;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.DeleteAsync($"formas-pagamento/{id}");
        return response.IsSuccessStatusCode;
    }
}
