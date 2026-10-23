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

    public async Task<List<AbastecimentoListItem>> GetAbastecimentosAsync(DateTime dataInicio, DateTime dataFim, int? veiculoId = null)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var query = $"abastecimentos?{ConsultaPeriodo.Montar(dataInicio, dataFim, veiculoId)}";
        var response = await client.GetFromJsonAsync<AbastecimentoListResponse>(query);
        return response?.Abastecimentos ?? new List<AbastecimentoListItem>();
    }

    /// <summary>Média km/l e custos por veículo no período, mais o consolidado da frota.</summary>
    public async Task<ConsumoResponse?> GetConsumoAsync(DateTime dataInicio, DateTime dataFim, int? veiculoId = null)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var query = $"abastecimentos/consumo?{ConsultaPeriodo.Montar(dataInicio, dataFim, veiculoId)}";
        return await client.GetFromJsonAsync<ConsumoResponse>(query);
    }

    /// <summary>
    /// Cria (id nulo) ou atualiza o abastecimento. Na criação a API lança a despesa no financeiro com
    /// o <paramref name="comprovante"/> (obrigatório); na edição, ele só é enviado para substituir o atual.
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
        LancamentoFinanceiroDados financeiro,
        FileResult? comprovante)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();

        await using var form = new FormularioMultipart()
            .Campo("veiculo_id", veiculoId)
            .Campo("data", data)
            .Campo("km", km)
            .Campo("litros", litros)
            .Campo("valor_total", valorTotal)
            .Campo("combustivel", combustivel)
            .Campo("tanque_cheio", tanqueCheio)
            .Campo("posto", posto)
            .Financeiro(financeiro);
        await form.ArquivoAsync("comprovante", comprovante);

        var response = id is int abastecimentoId
            ? await form.EnviarAsync(client, $"abastecimentos/{abastecimentoId}", atualizar: true)
            : await form.EnviarAsync(client, "abastecimentos");

        return await ApiSaveResult<AbastecimentoListItem>.FromResponseAsync<AbastecimentoResponse>(response, r => r.Abastecimento);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.DeleteAsync($"abastecimentos/{id}");
        return response.IsSuccessStatusCode;
    }
}
