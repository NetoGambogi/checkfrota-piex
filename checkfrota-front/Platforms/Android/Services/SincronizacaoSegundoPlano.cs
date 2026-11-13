using Android.Content;
using AndroidX.Work;
using checkfrota_front.Services;
using Microsoft.Extensions.DependencyInjection;

namespace checkfrota_front.Platforms.Android.Services;

/// <summary>
/// Agenda no WorkManager o envio da fila offline. O Android roda o <see cref="SincronizacaoWorker"/>
/// quando o aparelho tiver rede, mesmo com o app fechado ou depois de reiniciar o celular.
/// </summary>
public class SincronizacaoSegundoPlano : ISincronizacaoSegundoPlano
{
    private const string NomeTrabalho = "checkfrota-sincronizacao";

    public void Agendar()
    {
        var restricoes = new Constraints.Builder()
            .SetRequiredNetworkType(NetworkType.Connected!)
            .Build();

        var trabalho = new OneTimeWorkRequest.Builder(typeof(SincronizacaoWorker))
            .SetConstraints(restricoes)
            // Servidor fora ou sinal fraco: o Android tenta de novo em 30s, 1min, 2min… (até 5h).
            .SetBackoffCriteria(BackoffPolicy.Exponential!, TimeSpan.FromSeconds(30))
            .Build();

        // Keep: já agendado ou rodando, não cria outro (o que está rodando confere a fila no fim).
        WorkManager.GetInstance(Platform.AppContext)
            .EnqueueUniqueWork(NomeTrabalho, ExistingWorkPolicy.Keep!, (OneTimeWorkRequest)trabalho);
    }
}

/// <summary>Envia a fila offline em segundo plano; o Android o executa quando há rede.</summary>
public class SincronizacaoWorker : Worker
{
    public SincronizacaoWorker(Context context, WorkerParameters parametros) : base(context, parametros)
    {
    }

    // Já roda numa thread de segundo plano do WorkManager: pode esperar o envio terminar.
    public override Result DoWork()
    {
        try
        {
            var services = IPlatformApplication.Current?.Services;
            if (services is null) return Result.InvokeRetry()!;

            // Sem sessão (saiu ou expirou): a fila fica guardada e vai ao entrar de novo.
            if (SecureStorage.GetAsync("auth_token").GetAwaiter().GetResult() is null)
                return Result.InvokeSuccess()!;

            services.GetRequiredService<SincronizacaoService>().SincronizarAsync().GetAwaiter().GetResult();

            var restamPendentes = services.GetRequiredService<FilaOffline>().ListarAsync().GetAwaiter().GetResult()
                .Any(o => o.Situacao == SituacaoOperacao.Pendente);

            return restamPendentes ? Result.InvokeRetry()! : Result.InvokeSuccess()!;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            return Result.InvokeRetry()!;
        }
    }
}
