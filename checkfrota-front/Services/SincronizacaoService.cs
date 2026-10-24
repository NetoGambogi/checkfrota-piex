using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace checkfrota_front.Services;

public enum ResultadoEnvio
{
    Enviada,

    /// <summary>Sem rede ou servidor indisponível: continua na fila para uma nova tentativa.</summary>
    Pendente,

    Recusada,
}

/// <summary>
/// Envia a <see cref="FilaOffline"/> para a API, uma operação por vez e na ordem em que foram
/// feitas. Roda quando a internet volta, quando o app é aberto/retomado e logo após cada registro.
/// </summary>
public class SincronizacaoService
{
    private static readonly TimeSpan EsperaMaxima = TimeSpan.FromMinutes(5);

    private readonly FilaOffline _fila;
    private readonly IServiceProvider _services;

    // Um envio por vez: o km do hodômetro precisa chegar à API na ordem dos registros.
    private readonly SemaphoreSlim _enviando = new(1, 1);
    private CancellationTokenSource? _novaTentativa;
    private bool _iniciado;

    public SincronizacaoService(FilaOffline fila, IServiceProvider services)
    {
        _fila = fila;
        _services = services;
    }

    private static bool TemInternet => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

    public void Iniciar()
    {
        if (_iniciado) return;
        _iniciado = true;

        Connectivity.Current.ConnectivityChanged += (_, e) =>
        {
            if (e.NetworkAccess == NetworkAccess.Internet) Disparar();
        };

        Disparar();
    }

    /// <summary>Envia a fila em segundo plano, sem esperar.</summary>
    public void Disparar() => _ = SincronizarAsync();

    public async Task SincronizarAsync()
    {
        if (!TemInternet) return;

        await _enviando.WaitAsync();
        try
        {
            var falhas = 0;
            foreach (var operacao in await _fila.ListarAsync())
            {
                if (operacao.Situacao != SituacaoOperacao.Pendente) continue;

                var resultado = await ProcessarAsync(operacao, CancellationToken.None);
                if (resultado == ResultadoEnvio.Pendente)
                {
                    // Para aqui para não mandar as seguintes fora de ordem.
                    falhas = operacao.Tentativas;
                    break;
                }
            }

            if (falhas > 0) AgendarNovaTentativa(falhas);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            _enviando.Release();
        }
    }

    /// <summary>
    /// Tenta enviar já a operação recém-registrada, para o formulário mostrar uma recusa da API
    /// (ex: km fora da sequência) enquanto o motorista ainda pode corrigir. Sem rede, ou se demorar
    /// mais que <paramref name="limite"/>, ela fica na fila.
    /// </summary>
    public async Task<(ResultadoEnvio Resultado, string? Erro)> EnviarAgoraAsync(Guid id, TimeSpan limite)
    {
        if (!TemInternet) return (ResultadoEnvio.Pendente, null);

        using var cancelamento = new CancellationTokenSource(limite);
        try
        {
            await _enviando.WaitAsync(cancelamento.Token);
        }
        catch (OperationCanceledException)
        {
            return (ResultadoEnvio.Pendente, null);
        }

        try
        {
            var fila = await _fila.ListarAsync();

            // Operações anteriores ainda na fila vão primeiro (ordem do hodômetro): esta espera por elas.
            if (fila.TakeWhile(o => o.Id != id).Any(o => o.Situacao == SituacaoOperacao.Pendente))
            {
                Disparar();
                return (ResultadoEnvio.Pendente, null);
            }

            var operacao = fila.First(o => o.Id == id);
            var resultado = await ProcessarAsync(operacao, cancelamento.Token);
            if (resultado == ResultadoEnvio.Recusada)
            {
                // O formulário continua aberto para corrigir: não fica como pendência.
                await _fila.RemoverAsync(id);
            }
            else if (resultado == ResultadoEnvio.Pendente)
            {
                AgendarNovaTentativa(operacao.Tentativas);
            }

            return (resultado, operacao.Erro);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            return (ResultadoEnvio.Pendente, null);
        }
        finally
        {
            _enviando.Release();
        }
    }

    private async Task<ResultadoEnvio> ProcessarAsync(OperacaoPendente operacao, CancellationToken cancelamento)
    {
        // Transient: cada envio usa um HttpClient novo da factory.
        var motorista = _services.GetRequiredService<MotoristaService>();

        HttpResponseMessage resposta;
        try
        {
            resposta = await motorista.EnviarOperacaoAsync(operacao, cancelamento);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            return await ManterNaFilaAsync(operacao, "Sem conexão com o servidor.");
        }

        using (resposta)
        {
            if (resposta.IsSuccessStatusCode)
            {
                await motorista.ConfirmarOperacaoAsync(operacao, resposta);
                await _fila.RemoverAsync(operacao.Id);
                return ResultadoEnvio.Enviada;
            }

            // Sessão encerrada ou instabilidade do servidor: tenta de novo mais tarde.
            if (resposta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests
                || (int)resposta.StatusCode >= 500)
            {
                return await ManterNaFilaAsync(operacao, $"O servidor não respondeu ({(int)resposta.StatusCode}).");
            }

            operacao.Situacao = SituacaoOperacao.Recusada;
            operacao.Erro = await ApiSaveResult<object>.ReadErrorMessageAsync(resposta)
                ?? "O servidor recusou o registro.";
            await _fila.AtualizarAsync(operacao);
            return ResultadoEnvio.Recusada;
        }
    }

    private async Task<ResultadoEnvio> ManterNaFilaAsync(OperacaoPendente operacao, string erro)
    {
        operacao.Tentativas++;
        operacao.Erro = erro;
        await _fila.AtualizarAsync(operacao);
        return ResultadoEnvio.Pendente;
    }

    // Espera cada vez maior entre as tentativas (10s, 20s, 40s… até 5 min), para não gastar a
    // bateria e os dados de quem está com sinal fraco.
    private void AgendarNovaTentativa(int tentativas)
    {
        var espera = TimeSpan.FromSeconds(Math.Min(10 * Math.Pow(2, tentativas - 1), EsperaMaxima.TotalSeconds));

        _novaTentativa?.Cancel();
        var cancelamento = _novaTentativa = new CancellationTokenSource();

        _ = Task.Delay(espera, cancelamento.Token).ContinueWith(tarefa =>
        {
            if (!tarefa.IsCanceled) Disparar();
        }, TaskScheduler.Default);
    }
}
