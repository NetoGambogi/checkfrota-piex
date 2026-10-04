using checkfrota_front.Models;
using System.Net.Http.Json;

namespace checkfrota_front.Services;

public class ViagemService
{
    private readonly ApiAuthService _apiAuth;

    public ViagemService(ApiAuthService apiAuth)
    {
        _apiAuth = apiAuth;
    }

    public async Task<List<ViagemListItem>> GetViagensAsync()
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.GetFromJsonAsync<ViagemListResponse>("viagens");
        return response?.Viagens ?? new List<ViagemListItem>();
    }

    /// <summary>
    /// Cria (id nulo) ou atualiza a viagem. Com <paramref name="valorFrete"/> informado, a API lança
    /// (ou sincroniza) a receita no financeiro; sem ele, a receita pendente existente é excluída.
    /// </summary>
    public async Task<ApiSaveResult<ViagemListItem>> SaveAsync(
        int? id,
        int rotaId,
        int veiculoId,
        DateTime dataSaida,
        DateTime? dataChegada,
        int? kmSaida,
        int? kmChegada,
        string? cliente,
        string? observacoes,
        decimal? valorFrete,
        LancamentoFinanceiroDados? financeiro)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var payload = new
        {
            rota_id = rotaId,
            veiculo_id = veiculoId,
            data_saida = dataSaida.ToString("yyyy-MM-dd"),
            data_chegada = dataChegada?.ToString("yyyy-MM-dd"),
            km_saida = kmSaida,
            km_chegada = kmChegada,
            cliente,
            observacoes,
            valor_frete = valorFrete,
            categoria_financeira_id = financeiro?.CategoriaFinanceiraId,
            forma_pagamento_id = financeiro?.FormaPagamentoId,
            data_vencimento = financeiro?.DataVencimento.ToString("yyyy-MM-dd"),
            status = financeiro?.Status,
        };

        var response = id is int viagemId
            ? await client.PutAsJsonAsync($"viagens/{viagemId}", payload)
            : await client.PostAsJsonAsync("viagens", payload);

        return await ApiSaveResult<ViagemListItem>.FromResponseAsync<ViagemResponse>(response, r => r.Viagem);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.DeleteAsync($"viagens/{id}");
        return response.IsSuccessStatusCode;
    }
}
