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

    // Com sinal fraco o aparelho diz que tem internet, mas a resposta não chega: passado esse
    // tempo, a consulta usa o que está salvo no aparelho em vez de esperar os 100s do HttpClient.
    private static readonly TimeSpan LimiteConsulta = TimeSpan.FromSeconds(12);

    // Intervalo mínimo entre dois pré-carregamentos do que o motorista usa offline.
    private static readonly TimeSpan IntervaloPreCarregamento = TimeSpan.FromMinutes(10);
    private static DateTime _ultimoPreCarregamento = DateTime.MinValue;

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
    private async Task<T> ConsultarAsync<T>(string chave, Func<HttpClient, CancellationToken, Task<T>> consulta)
    {
        if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
        {
            try
            {
                using var tempoLimite = new CancellationTokenSource(LimiteConsulta);
                var resposta = await consulta(await _apiAuth.GetAuthenticatedClientAsync(), tempoLimite.Token);
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

    /// <summary>
    /// Baixa e guarda no aparelho o que os formulários do motorista precisam (veículos),
    /// para eles funcionarem sem rede mesmo que nunca tenham sido abertos com internet. Roda em
    /// segundo plano ao abrir o início; falhas são ignoradas, a tela segue com o que já tem.
    /// </summary>
    public async Task PreCarregarAsync()
    {
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet
            || DateTime.UtcNow - _ultimoPreCarregamento < IntervaloPreCarregamento)
        {
            return;
        }

        _ultimoPreCarregamento = DateTime.UtcNow;

        try
        {
            await GetVeiculosAsync();
        }
        catch (Exception ex)
        {
            // Sem rede de verdade: tenta de novo na próxima abertura.
            _ultimoPreCarregamento = DateTime.MinValue;
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    /// <summary>Veículos ativos, com o km atual como referência para o hodômetro.</summary>
    public async Task<List<VeiculoListItem>> GetVeiculosAsync()
    {
        return await ConsultarAsync("motorista-veiculos", async (client, cancelamento) =>
            (await client.GetFromJsonAsync<VeiculoListResponse>("motorista/veiculos", cancelamento))?.Veiculos ?? new List<VeiculoListItem>());
    }

    /// <summary>
    /// Página das viagens atribuídas ao motorista pela gestão da frota; <paramref name="status"/> é
    /// "em_andamento" ou "concluida".
    /// </summary>
    public async Task<ViagemPaginadaResponse> GetViagensAsync(string status, int pagina = 1, int porPagina = 15)
    {
        return await ConsultarAsync($"motorista-viagens-{status}-{pagina}-{porPagina}", async (client, cancelamento) =>
            await client.GetFromJsonAsync<ViagemPaginadaResponse>(
                $"motorista/viagens?status={status}&page={pagina}&por_pagina={porPagina}", cancelamento) ?? new ViagemPaginadaResponse());
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

    public async Task<List<AbastecimentoListItem>> GetAbastecimentosAsync()
    {
        return await ConsultarAsync(CacheAbastecimentos, async (client, cancelamento) =>
            (await client.GetFromJsonAsync<AbastecimentoListResponse>("motorista/abastecimentos", cancelamento))?.Abastecimentos
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
        return await ConsultarAsync("motorista-ocorrencias", async (client, cancelamento) =>
            (await client.GetFromJsonAsync<OcorrenciaListResponse>("motorista/ocorrencias", cancelamento))?.Ocorrencias
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
