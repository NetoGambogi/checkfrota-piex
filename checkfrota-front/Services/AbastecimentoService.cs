using checkfrota_front.Models;
using System.Net.Http.Json;

namespace checkfrota_front.Services;

public class AbastecimentoService
{
    private readonly ApiAuthService _apiAuth;

    public AbastecimentoService(ApiAuthService apiAuth)
    {
        _apiAuth = apiAuth;
    }

    public async Task<List<AbastecimentoListItem>> GetAbastecimentosAsync(int? veiculoId = null)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var query = veiculoId is int id ? $"abastecimentos?veiculo_id={id}" : "abastecimentos";
        var response = await client.GetFromJsonAsync<AbastecimentoListResponse>(query);
        return response?.Abastecimentos ?? new List<AbastecimentoListItem>();
    }

    /// <summary>Média km/l e custos por veículo, mais o consolidado da frota.</summary>
    public async Task<ConsumoResponse?> GetConsumoAsync(int? veiculoId = null)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var query = veiculoId is int id ? $"abastecimentos/consumo?veiculo_id={id}" : "abastecimentos/consumo";
        return await client.GetFromJsonAsync<ConsumoResponse>(query);
    }

    /// <summary>
    /// Cria (id nulo) ou atualiza o abastecimento. Na criação a API lança a despesa no financeiro.
    /// </summary>
    public async Task<ApiSaveResult<AbastecimentoListItem>> SaveAsync(
        int? id,
        int veiculoId,
        DateTime data,
        int km,
        decimal litros,
        decimal valorTotal,
        string combustivel,
        bool tanqueCheio,
        string? posto,
        LancamentoFinanceiroDados financeiro)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var payload = new
        {
            veiculo_id = veiculoId,
            data = data.ToString("yyyy-MM-dd"),
            km,
            litros,
            valor_total = valorTotal,
            combustivel,
            tanque_cheio = tanqueCheio,
            posto,
            categoria_financeira_id = financeiro.CategoriaFinanceiraId,
            forma_pagamento_id = financeiro.FormaPagamentoId,
            data_vencimento = financeiro.DataVencimento.ToString("yyyy-MM-dd"),
            status = financeiro.Status,
        };

        var response = id is int abastecimentoId
            ? await client.PutAsJsonAsync($"abastecimentos/{abastecimentoId}", payload)
            : await client.PostAsJsonAsync("abastecimentos", payload);

        return await ApiSaveResult<AbastecimentoListItem>.FromResponseAsync<AbastecimentoResponse>(response, r => r.Abastecimento);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.DeleteAsync($"abastecimentos/{id}");
        return response.IsSuccessStatusCode;
    }
}
