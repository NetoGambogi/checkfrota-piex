using checkfrota_front.Models;
using System.Net.Http.Json;

namespace checkfrota_front.Services;

/// <summary>
/// Endpoints do app do motorista (/api/motorista/*): só as próprias viagens e abastecimentos,
/// sem frete nem lançamentos financeiros, que ficam com a frota/financeiro.
/// </summary>
public class MotoristaService
{
    private readonly ApiAuthService _apiAuth;

    public MotoristaService(ApiAuthService apiAuth)
    {
        _apiAuth = apiAuth;
    }

    /// <summary>Rotas ativas para escolher ao iniciar uma viagem.</summary>
    public async Task<List<RotaListItem>> GetRotasAsync()
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.GetFromJsonAsync<RotaListResponse>("motorista/rotas");
        return response?.Rotas ?? new List<RotaListItem>();
    }

    /// <summary>Veículos ativos, com o km atual como referência para o hodômetro.</summary>
    public async Task<List<VeiculoListItem>> GetVeiculosAsync()
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.GetFromJsonAsync<VeiculoListResponse>("motorista/veiculos");
        return response?.Veiculos ?? new List<VeiculoListItem>();
    }

    /// <summary>Página de viagens do motorista; <paramref name="status"/> é "em_andamento" ou "concluida".</summary>
    public async Task<ViagemPaginadaResponse> GetViagensAsync(string status, int pagina = 1, int porPagina = 15)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.GetFromJsonAsync<ViagemPaginadaResponse>(
            $"motorista/viagens?status={status}&page={pagina}&por_pagina={porPagina}");
        return response ?? new ViagemPaginadaResponse();
    }

    /// <summary>Inicia (id nulo) ou edita uma viagem em andamento.</summary>
    public async Task<ApiSaveResult<ViagemListItem>> SaveViagemAsync(
        int? id,
        int rotaId,
        int veiculoId,
        DateTime dataSaida,
        int? kmSaida,
        string? cliente,
        string? observacoes)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var payload = new
        {
            rota_id = rotaId,
            veiculo_id = veiculoId,
            data_saida = dataSaida.ToString("yyyy-MM-dd"),
            km_saida = kmSaida,
            cliente,
            observacoes,
        };

        var response = id is int viagemId
            ? await client.PutAsJsonAsync($"motorista/viagens/{viagemId}", payload)
            : await client.PostAsJsonAsync("motorista/viagens", payload);

        return await ApiSaveResult<ViagemListItem>.FromResponseAsync<ViagemResponse>(response, r => r.Viagem);
    }

    /// <summary>Registra a chegada da viagem e, opcionalmente, o km final.</summary>
    public async Task<ApiSaveResult<ViagemListItem>> ConcluirViagemAsync(int id, DateTime dataChegada, int? kmChegada)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var payload = new
        {
            data_chegada = dataChegada.ToString("yyyy-MM-dd"),
            km_chegada = kmChegada,
        };

        var response = await client.PatchAsJsonAsync($"motorista/viagens/{id}/concluir", payload);
        return await ApiSaveResult<ViagemListItem>.FromResponseAsync<ViagemResponse>(response, r => r.Viagem);
    }

    public async Task<bool> DeleteViagemAsync(int id)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.DeleteAsync($"motorista/viagens/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<List<AbastecimentoListItem>> GetAbastecimentosAsync()
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.GetFromJsonAsync<AbastecimentoListResponse>("motorista/abastecimentos");
        return response?.Abastecimentos ?? new List<AbastecimentoListItem>();
    }

    /// <summary>
    /// Registra um abastecimento com o comprovante (foto ou PDF, obrigatório), num único envio.
    /// Não gera despesa: o financeiro lança depois, conferindo pelo comprovante.
    /// </summary>
    public async Task<ApiSaveResult<AbastecimentoListItem>> RegistrarAbastecimentoAsync(
        int veiculoId,
        DateTime data,
        int km,
        decimal litros,
        decimal valorTotal,
        string combustivel,
        bool tanqueCheio,
        string? posto,
        FileResult comprovante)
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
            .Campo("posto", posto);
        await form.ArquivoAsync("comprovante", comprovante);

        var response = await form.EnviarAsync(client, "motorista/abastecimentos");
        return await ApiSaveResult<AbastecimentoListItem>.FromResponseAsync<AbastecimentoResponse>(response, r => r.Abastecimento);
    }

    /// <summary>Ocorrências relatadas pelo motorista, com o status do tratamento pela frota.</summary>
    public async Task<List<OcorrenciaItem>> GetOcorrenciasAsync()
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();
        var response = await client.GetFromJsonAsync<OcorrenciaListResponse>("motorista/ocorrencias");
        return response?.Ocorrencias ?? new List<OcorrenciaItem>();
    }

    /// <summary>Relata um problema na viagem; a gestão da frota é avisada por notificação.</summary>
    public async Task<ApiSaveResult<OcorrenciaItem>> RegistrarOcorrenciaAsync(
        int viagemId,
        string tipo,
        string gravidade,
        int km,
        string descricao,
        FileResult? foto)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();

        await using var form = new FormularioMultipart()
            .Campo("tipo", tipo)
            .Campo("gravidade", gravidade)
            .Campo("km", km)
            .Campo("descricao", descricao);
        await form.ArquivoAsync("foto", foto);

        var response = await form.EnviarAsync(client, $"motorista/viagens/{viagemId}/ocorrencias");
        return await ApiSaveResult<OcorrenciaItem>.FromResponseAsync<OcorrenciaResponse>(response, r => r.Ocorrencia);
    }
}
