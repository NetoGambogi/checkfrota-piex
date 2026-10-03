using checkfrota_front.Models;
using System.Net.Http.Json;

namespace checkfrota_front.Services;

public class RotaService
{
    private readonly ApiAuthService _apiAuth;

    public RotaService(ApiAuthService apiAuth)
    {
        _apiAuth = apiAuth;
    }

    public async Task<List<RotaListItem>> GetRotasAsync(string situacao = "ativos")
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.GetFromJsonAsync<RotaListResponse>($"rotas?situacao={Uri.EscapeDataString(situacao)}");
        return response?.Rotas ?? new List<RotaListItem>();
    }

    public async Task<ApiSaveResult<RotaListItem>> SaveAsync(int? id, string nome, string origem, string destino, int kmAproximado, string? observacoes)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var payload = new
        {
            nome,
            origem,
            destino,
            km_aproximado = kmAproximado,
            observacoes,
        };

        var response = id is int rotaId
            ? await client.PutAsJsonAsync($"rotas/{rotaId}", payload)
            : await client.PostAsJsonAsync("rotas", payload);

        return await ApiSaveResult<RotaListItem>.FromResponseAsync<RotaResponse>(response, r => r.Rota);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.DeleteAsync($"rotas/{id}");
        return response.IsSuccessStatusCode;
    }
}
