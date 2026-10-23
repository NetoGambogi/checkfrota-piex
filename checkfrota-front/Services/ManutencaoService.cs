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
    /// Cria (id nulo) ou atualiza a manutenção. Na criação a API lança a despesa no financeiro com o
    /// <paramref name="comprovante"/> (obrigatório); na edição, ele só é enviado para substituir o atual.
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
        LancamentoFinanceiroDados financeiro,
        FileResult? comprovante)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();

        await using var form = new FormularioMultipart()
            .Campo("veiculo_id", veiculoId)
            .Campo("tipo", tipo)
            .Campo("descricao", descricao)
            .Campo("data", data)
            .Campo("km", km)
            .Campo("oficina", oficina)
            .Campo("valor", valor)
            .Financeiro(financeiro);
        await form.ArquivoAsync("comprovante", comprovante);

        var response = id is int manutencaoId
            ? await form.EnviarAsync(client, $"manutencoes/{manutencaoId}", atualizar: true)
            : await form.EnviarAsync(client, "manutencoes");

        return await ApiSaveResult<ManutencaoListItem>.FromResponseAsync<ManutencaoResponse>(response, r => r.Manutencao);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.DeleteAsync($"manutencoes/{id}");
        return response.IsSuccessStatusCode;
    }
}
