using checkfrota_front.Models;
using System.Net.Http.Json;

namespace checkfrota_front.Services;

/// <summary>
/// Endpoints do app do motorista (/api/motorista/*): só as próprias viagens e abastecimentos,
/// sem frete nem lançamentos financeiros, que ficam com a frota/financeiro.
/// Funciona offline: as consultas caem no <see cref="CacheLocal"/> sem rede, e o abastecimento
/// vai para a <see cref="FilaOffline"/>, enviada pelo <see cref="SincronizacaoService"/>.
/// </summary>
public class MotoristaService
{
    private const string CacheAbastecimentos = "motorista-abastecimentos";

    // Quanto o formulário espera pelo envio antes de deixar o registro na fila.
    private static readonly TimeSpan LimiteEnvioImediato = TimeSpan.FromSeconds(20);

    private readonly ApiAuthService _apiAuth;
    private readonly CacheLocal _cache;
    private readonly FilaOffline _fila;
    private readonly SincronizacaoService _sincronizacao;

    public MotoristaService(ApiAuthService apiAuth, CacheLocal cache, FilaOffline fila, SincronizacaoService sincronizacao)
    {
        _apiAuth = apiAuth;
        _cache = cache;
        _fila = fila;
        _sincronizacao = sincronizacao;
    }

    /// <summary>
    /// Consulta a API e guarda a resposta; sem rede, devolve a última resposta guardada (ou lança
    /// o erro, se nunca houve uma).
    /// </summary>
    private async Task<T> ConsultarAsync<T>(string chave, Func<HttpClient, Task<T>> consulta)
    {
        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
        {
            try
            {
                var resposta = await consulta(await _apiAuth.GetAuthenticatedClientAsync());
                await _cache.SalvarAsync(chave, resposta);
                return resposta;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        return await _cache.LerAsync<T>(chave)
            ?? throw new HttpRequestException("Sem conexão e sem dados salvos no aparelho.");
    }

    /// <summary>Rotas ativas para escolher ao iniciar uma viagem.</summary>
    public async Task<List<RotaListItem>> GetRotasAsync()
    {
        return await ConsultarAsync("motorista-rotas", async client =>
            (await client.GetFromJsonAsync<RotaListResponse>("motorista/rotas"))?.Rotas ?? new List<RotaListItem>());
    }

    /// <summary>Veículos ativos, com o km atual como referência para o hodômetro.</summary>
    public async Task<List<VeiculoListItem>> GetVeiculosAsync()
    {
        return await ConsultarAsync("motorista-veiculos", async client =>
            (await client.GetFromJsonAsync<VeiculoListResponse>("motorista/veiculos"))?.Veiculos ?? new List<VeiculoListItem>());
    }

    /// <summary>Página de viagens do motorista; <paramref name="status"/> é "em_andamento" ou "concluida".</summary>
    public async Task<ViagemPaginadaResponse> GetViagensAsync(string status, int pagina = 1, int porPagina = 15)
    {
        return await ConsultarAsync($"motorista-viagens-{status}-{pagina}-{porPagina}", async client =>
            await client.GetFromJsonAsync<ViagemPaginadaResponse>(
                $"motorista/viagens?status={status}&page={pagina}&por_pagina={porPagina}") ?? new ViagemPaginadaResponse());
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
        return await ConsultarAsync(CacheAbastecimentos, async client =>
            (await client.GetFromJsonAsync<AbastecimentoListResponse>("motorista/abastecimentos"))?.Abastecimentos
                ?? new List<AbastecimentoListItem>());
    }

    /// <summary>
    /// Registra um abastecimento com o comprovante (foto ou PDF, obrigatório). Fica guardado no
    /// aparelho e é enviado na hora, se houver rede, ou assim que ela voltar. Uma recusa da API no
    /// envio imediato (ex: km fora da sequência) volta como erro para o motorista corrigir.
    /// Não gera despesa: o financeiro lança depois, conferindo pelo comprovante.
    /// </summary>
    public async Task<(ResultadoEnvio Resultado, string? Erro)> RegistrarAbastecimentoAsync(
        VeiculoResumo veiculo,
        DateTime data,
        int km,
        decimal litros,
        decimal valorTotal,
        string combustivel,
        bool tanqueCheio,
        string? posto,
        FileResult comprovante)
    {
        var operacao = new OperacaoPendente
        {
            Tipo = OperacaoPendente.TipoAbastecimento,
            Abastecimento = new RegistroAbastecimento(veiculo, data, km, litros, valorTotal, combustivel, tanqueCheio, posto),
        };

        await _fila.EnfileirarAsync(operacao, comprovante);
        return await _sincronizacao.EnviarAgoraAsync(operacao.Id, LimiteEnvioImediato);
    }

    /// <summary>Abastecimentos registrados no aparelho que a API ainda não confirmou.</summary>
    public async Task<List<OperacaoPendente>> GetAbastecimentosPendentesAsync() =>
        (await _fila.ListarAsync()).Where(o => o.Tipo == OperacaoPendente.TipoAbastecimento).ToList();

    public Task DescartarPendenciaAsync(Guid id) => _fila.RemoverAsync(id);

    /// <summary>Envia uma operação da fila; o <see cref="SincronizacaoService"/> trata a resposta.</summary>
    internal async Task<HttpResponseMessage> EnviarOperacaoAsync(OperacaoPendente operacao, CancellationToken cancelamento)
    {
        var client = await _apiAuth.GetAuthenticatedClientAsync();

        switch (operacao.Tipo)
        {
            case OperacaoPendente.TipoAbastecimento when operacao.Abastecimento is { } dados && operacao.Anexo is not null:
            {
                await using var form = new FormularioMultipart()
                    .Campo("client_uuid", operacao.Id.ToString())
                    .Campo("veiculo_id", dados.Veiculo.Id)
                    .Campo("data", dados.Data)
                    .Campo("km", dados.Km)
                    .Campo("litros", dados.Litros)
                    .Campo("valor_total", dados.ValorTotal)
                    .Campo("combustivel", dados.Combustivel)
                    .Campo("tanque_cheio", dados.TanqueCheio)
                    .Campo("posto", dados.Posto);
                await form.ArquivoAsync("comprovante", new FileResult(operacao.Anexo, operacao.AnexoContentType));

                return await form.EnviarAsync(client, "motorista/abastecimentos", cancelamento: cancelamento);
            }

            default:
                throw new InvalidOperationException($"Operação offline inválida: {operacao.Tipo}");
        }
    }

    /// <summary>
    /// Põe o item confirmado pela API no cache, para ele não sumir da lista aberta sem rede entre
    /// sair da fila e a próxima consulta.
    /// </summary>
    internal async Task ConfirmarOperacaoAsync(OperacaoPendente operacao, HttpResponseMessage resposta)
    {
        if (operacao.Tipo != OperacaoPendente.TipoAbastecimento) return;

        var salvo = (await resposta.Content.ReadFromJsonAsync<AbastecimentoResponse>())?.Abastecimento;
        if (salvo is null) return;

        var cache = await _cache.LerAsync<List<AbastecimentoListItem>>(CacheAbastecimentos) ?? new();
        cache.RemoveAll(a => a.Id == salvo.Id);
        cache.Insert(0, salvo);
        await _cache.SalvarAsync(CacheAbastecimentos, cache);
    }

    /// <summary>Ocorrências relatadas pelo motorista, com o status do tratamento pela frota.</summary>
    public async Task<List<OcorrenciaItem>> GetOcorrenciasAsync()
    {
        return await ConsultarAsync("motorista-ocorrencias", async client =>
            (await client.GetFromJsonAsync<OcorrenciaListResponse>("motorista/ocorrencias"))?.Ocorrencias
                ?? new List<OcorrenciaItem>());
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
