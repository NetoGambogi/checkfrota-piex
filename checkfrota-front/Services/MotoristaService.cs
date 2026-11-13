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
    private const string CacheOcorrencias = "motorista-ocorrencias";

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
    /// "em_andamento" ou "concluida". Conclusões ainda na fila já aparecem como concluídas.
    /// </summary>
    public async Task<ViagemPaginadaResponse> GetViagensAsync(string status, int pagina = 1, int porPagina = 15)
    {
        var resposta = await ConsultarAsync($"motorista-viagens-{status}-{pagina}-{porPagina}", async (client, cancelamento) =>
            await client.GetFromJsonAsync<ViagemPaginadaResponse>(
                $"motorista/viagens?status={status}&page={pagina}&por_pagina={porPagina}", cancelamento) ?? new ViagemPaginadaResponse());

        return await AplicarConclusoesPendentesAsync(resposta, status, pagina);
    }

    private async Task<ViagemPaginadaResponse> AplicarConclusoesPendentesAsync(ViagemPaginadaResponse resposta, string status, int pagina)
    {
        var conclusoes = (await _fila.ListarAsync())
            .Where(o => o.Tipo == OperacaoPendente.TipoConclusaoViagem && o.Situacao == SituacaoOperacao.Pendente && o.ConclusaoViagem is not null)
            .Select(o => o.ConclusaoViagem!)
            .ToList();
        if (conclusoes.Count == 0) return resposta;

        var ids = conclusoes.Select(c => c.Viagem.Id).ToHashSet();
        var viagens = resposta.Viagens.Where(v => !ids.Contains(v.Id)).ToList();

        if (status == "em_andamento")
        {
            resposta.Paginacao.Total = Math.Max(0, resposta.Paginacao.Total - (resposta.Viagens.Count - viagens.Count));
        }
        else if (status == "concluida" && pagina == 1)
        {
            viagens.InsertRange(0, conclusoes.Select(c => c.ComoViagemConcluida()));
        }

        resposta.Viagens = viagens;
        return resposta;
    }

    /// <summary>
    /// Registra a chegada da viagem e, opcionalmente, o km final. Fica guardada no aparelho e é
    /// enviada na hora, se houver rede, ou assim que ela voltar.
    /// </summary>
    public async Task<(ResultadoEnvio Resultado, string? Erro)> ConcluirViagemAsync(ViagemListItem viagem, DateTime dataChegada, int? kmChegada)
    {
        var operacao = new OperacaoPendente
        {
            Tipo = OperacaoPendente.TipoConclusaoViagem,
            ConclusaoViagem = new RegistroConclusaoViagem(viagem, dataChegada, kmChegada),
        };

        // Uma tentativa anterior recusada (a viagem voltou para "em andamento") é substituída por esta.
        foreach (var anterior in (await _fila.ListarAsync())
            .Where(o => o.Tipo == OperacaoPendente.TipoConclusaoViagem && o.ConclusaoViagem?.Viagem.Id == viagem.Id))
        {
            await _fila.RemoverAsync(anterior.Id);
        }

        await _fila.EnfileirarAsync(operacao, null);
        return await _sincronizacao.EnviarAgoraAsync(operacao.Id, LimiteEnvioImediato);
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

    /// <summary>Ocorrências registradas no aparelho que a API ainda não confirmou.</summary>
    public async Task<List<OperacaoPendente>> GetOcorrenciasPendentesAsync() =>
        (await _fila.ListarAsync()).Where(o => o.Tipo == OperacaoPendente.TipoOcorrencia).ToList();

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

            case OperacaoPendente.TipoOcorrencia when operacao.Ocorrencia is { } dados:
            {
                await using var form = new FormularioMultipart()
                    .Campo("client_uuid", operacao.Id.ToString())
                    .Campo("tipo", dados.Tipo)
                    .Campo("gravidade", dados.Gravidade)
                    .Campo("km", dados.Km)
                    .Campo("descricao", dados.Descricao);
                await form.ArquivoAsync("foto", operacao.Anexo is null ? null : new FileResult(operacao.Anexo, operacao.AnexoContentType));

                return await form.EnviarAsync(client, $"motorista/viagens/{dados.Viagem.Id}/ocorrencias", cancelamento: cancelamento);
            }

            case OperacaoPendente.TipoConclusaoViagem when operacao.ConclusaoViagem is { } dados:
            {
                var payload = new
                {
                    data_chegada = dados.DataChegada.ToString("yyyy-MM-dd"),
                    km_chegada = dados.KmChegada,
                };

                return await client.PatchAsJsonAsync($"motorista/viagens/{dados.Viagem.Id}/concluir", payload, cancelamento);
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
        switch (operacao.Tipo)
        {
            case OperacaoPendente.TipoAbastecimento:
            {
                var salvo = (await resposta.Content.ReadFromJsonAsync<AbastecimentoResponse>())?.Abastecimento;
                if (salvo is null) return;

                var cache = await _cache.LerAsync<List<AbastecimentoListItem>>(CacheAbastecimentos) ?? new();
                cache.RemoveAll(a => a.Id == salvo.Id);
                cache.Insert(0, salvo);
                await _cache.SalvarAsync(CacheAbastecimentos, cache);
                break;
            }

            case OperacaoPendente.TipoOcorrencia:
            {
                var salva = (await resposta.Content.ReadFromJsonAsync<OcorrenciaResponse>())?.Ocorrencia;
                if (salva is null) return;

                var cache = await _cache.LerAsync<List<OcorrenciaItem>>(CacheOcorrencias) ?? new();
                cache.RemoveAll(o => o.Id == salva.Id);
                cache.Insert(0, salva);
                await _cache.SalvarAsync(CacheOcorrencias, cache);
                break;
            }
        }
    }

    /// <summary>Ocorrências relatadas pelo motorista, com o status do tratamento pela frota.</summary>
    public async Task<List<OcorrenciaItem>> GetOcorrenciasAsync()
    {
        return await ConsultarAsync(CacheOcorrencias, async (client, cancelamento) =>
            (await client.GetFromJsonAsync<OcorrenciaListResponse>("motorista/ocorrencias", cancelamento))?.Ocorrencias
                ?? new List<OcorrenciaItem>());
    }

    /// <summary>
    /// Relata um problema na viagem; a gestão da frota é avisada por notificação. Fica guardada no
    /// aparelho (com a foto) e é enviada na hora, se houver rede, ou assim que ela voltar.
    /// </summary>
    public async Task<(ResultadoEnvio Resultado, string? Erro)> RegistrarOcorrenciaAsync(
        ViagemListItem viagem,
        string tipo,
        string tipoLabel,
        string gravidade,
        int km,
        string descricao,
        FileResult? foto)
    {
        var operacao = new OperacaoPendente
        {
            Tipo = OperacaoPendente.TipoOcorrencia,
            Ocorrencia = new RegistroOcorrencia(viagem, tipo, tipoLabel, gravidade, km, descricao),
        };

        await _fila.EnfileirarAsync(operacao, foto);
        return await _sincronizacao.EnviarAgoraAsync(operacao.Id, LimiteEnvioImediato);
    }
}
