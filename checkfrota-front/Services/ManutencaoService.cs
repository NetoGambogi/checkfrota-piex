using checkfrota_front.Models;
using System.Net.Http.Json;

namespace checkfrota_front.Services;

public class ManutencaoService
{
    private readonly ApiAuthService _apiAuth;

    public ManutencaoService(ApiAuthService apiAuth)
    {
        _apiAuth = apiAuth;
    }

    public async Task<List<ManutencaoListItem>> GetManutencoesAsync(DateTime dataInicio, DateTime dataFim, int? veiculoId = null)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var query = $"manutencoes?{ConsultaPeriodo.Montar(dataInicio, dataFim, veiculoId)}";
        var response = await client.GetFromJsonAsync<ManutencaoListResponse>(query);
        return response?.Manutencoes ?? new List<ManutencaoListItem>();
    }

    /// <summary>
    /// Cria (id nulo) ou atualiza a manutenção. Na criação a API lança a despesa no financeiro.
    /// </summary>
    public async Task<ApiSaveResult<ManutencaoListItem>> SaveAsync(
        int? id,
        int veiculoId,
        string tipo,
        string descricao,
        DateTime data,
        int? km,
        string? oficina,
        decimal valor,
        LancamentoFinanceiroDados financeiro)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var payload = new
        {
            veiculo_id = veiculoId,
            tipo,
            descricao,
            data = data.ToString("yyyy-MM-dd"),
            km,
            oficina,
            valor,
            categoria_financeira_id = financeiro.CategoriaFinanceiraId,
            forma_pagamento_id = financeiro.FormaPagamentoId,
            data_vencimento = financeiro.DataVencimento.ToString("yyyy-MM-dd"),
            status = financeiro.Status,
        };

        var response = id is int manutencaoId
            ? await client.PutAsJsonAsync($"manutencoes/{manutencaoId}", payload)
            : await client.PostAsJsonAsync("manutencoes", payload);

        return await ApiSaveResult<ManutencaoListItem>.FromResponseAsync<ManutencaoResponse>(response, r => r.Manutencao);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.DeleteAsync($"manutencoes/{id}");
        return response.IsSuccessStatusCode;
    }
}
