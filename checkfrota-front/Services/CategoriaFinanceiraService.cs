using checkfrota_front.Models;
using System.Net.Http.Json;

namespace checkfrota_front.Services;

public class CategoriaFinanceiraService
{
    private readonly ApiAuthService _apiAuth;

    public CategoriaFinanceiraService(ApiAuthService apiAuth)
    {
        _apiAuth = apiAuth;
    }

    public async Task<List<CategoriaFinanceiraListItem>> GetCategoriasAsync(string tipo, string status = "ativos")
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var query = $"categorias-financeiras?tipo={Uri.EscapeDataString(tipo)}&status={Uri.EscapeDataString(status)}";
        var response = await client.GetFromJsonAsync<CategoriaFinanceiraListResponse>(query);
        return response?.Categorias ?? new List<CategoriaFinanceiraListItem>();
    }

    public async Task<CategoriaFinanceiraListItem?> CreateAsync(string nome, string tipo, string? descricao, string icone)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.PostAsJsonAsync("categorias-financeiras", new { nome, tipo, descricao, icone });
        if (!response.IsSuccessStatusCode) return null;

        var result = await response.Content.ReadFromJsonAsync<CategoriaFinanceiraResponse>();
        return result?.Categoria;
    }

    public async Task<CategoriaFinanceiraListItem?> UpdateAsync(int id, string nome, string tipo, string? descricao, string icone)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.PutAsJsonAsync($"categorias-financeiras/{id}", new { nome, tipo, descricao, icone });
        if (!response.IsSuccessStatusCode) return null;

        var result = await response.Content.ReadFromJsonAsync<CategoriaFinanceiraResponse>();
        return result?.Categoria;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.DeleteAsync($"categorias-financeiras/{id}");
        return response.IsSuccessStatusCode;
    }
}
