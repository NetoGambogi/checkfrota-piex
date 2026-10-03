using checkfrota_front.Models;
using System.Net.Http.Json;

namespace checkfrota_front.Services;

public class VeiculoService
{
    private readonly ApiAuthService _apiAuth;

    public VeiculoService(ApiAuthService apiAuth)
    {
        _apiAuth = apiAuth;
    }

    public async Task<List<VeiculoListItem>> GetVeiculosAsync(string situacao = "ativos")
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.GetFromJsonAsync<VeiculoListResponse>($"veiculos?situacao={Uri.EscapeDataString(situacao)}");
        return response?.Veiculos ?? new List<VeiculoListItem>();
    }

    public async Task<ApiSaveResult<VeiculoListItem>> SaveAsync(int? id, VeiculoListItem veiculo)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var payload = new
        {
            placa = veiculo.Placa,
            tipo = veiculo.Tipo,
            marca = veiculo.Marca,
            modelo = veiculo.Modelo,
            ano = veiculo.Ano,
            renavam = veiculo.Renavam,
            km_atual = veiculo.KmAtual,
            observacoes = veiculo.Observacoes,
        };

        var response = id is int veiculoId
            ? await client.PutAsJsonAsync($"veiculos/{veiculoId}", payload)
            : await client.PostAsJsonAsync("veiculos", payload);

        return await ApiSaveResult<VeiculoListItem>.FromResponseAsync<VeiculoResponse>(response, r => r.Veiculo);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.DeleteAsync($"veiculos/{id}");
        return response.IsSuccessStatusCode;
    }
}
