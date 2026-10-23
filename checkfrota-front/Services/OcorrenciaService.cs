using checkfrota_front.Models;
using System.Net.Http.Json;

namespace checkfrota_front.Services;

/// <summary>Ocorrências relatadas pelos motoristas, vistas e tratadas pela gestão da frota.</summary>
public class OcorrenciaService
{
    private readonly ApiAuthService _apiAuth;

    public OcorrenciaService(ApiAuthService apiAuth)
    {
        _apiAuth = apiAuth;
    }

    /// <summary><paramref name="status"/>: "aberta", "resolvida" ou "todas".</summary>
    public async Task<OcorrenciaListResponse> GetOcorrenciasAsync(string status = "aberta")
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.GetFromJsonAsync<OcorrenciaListResponse>($"ocorrencias?status={status}");
        return response ?? new OcorrenciaListResponse();
    }

    public async Task<ApiSaveResult<OcorrenciaItem>> ResolverAsync(int id, string? observacao)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.PatchAsJsonAsync($"ocorrencias/{id}/resolver", new { observacao_resolucao = observacao });
        return await ApiSaveResult<OcorrenciaItem>.FromResponseAsync<OcorrenciaResponse>(response, r => r.Ocorrencia);
    }

    public async Task<ApiSaveResult<OcorrenciaItem>> ReabrirAsync(int id)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.PatchAsync($"ocorrencias/{id}/reabrir", null);
        return await ApiSaveResult<OcorrenciaItem>.FromResponseAsync<OcorrenciaResponse>(response, r => r.Ocorrencia);
    }
}
