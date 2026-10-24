using CommunityToolkit.Mvvm.Messaging;
using System.Text.Json;

namespace checkfrota_front.Services;

/// <summary>Avisa as telas abertas que a fila mudou (item enfileirado, enviado ou recusado).</summary>
public sealed record FilaOfflineAlterada;

/// <summary>
/// Fila das ações do motorista feitas no aparelho, persistida em JSON para sobreviver ao app
/// fechado. A ordem de criação é a ordem de envio (o km do hodômetro precisa chegar em sequência).
/// </summary>
public class FilaOffline
{
    private static readonly string Arquivo = Path.Combine(FileSystem.AppDataDirectory, "fila-offline.json");
    private static readonly string PastaAnexos = Path.Combine(FileSystem.AppDataDirectory, "anexos-pendentes");

    private readonly SemaphoreSlim _trava = new(1, 1);
    private List<OperacaoPendente>? _operacoes;

    public async Task<IReadOnlyList<OperacaoPendente>> ListarAsync()
    {
        await _trava.WaitAsync();
        try
        {
            return (await CarregarAsync()).ToList();
        }
        finally
        {
            _trava.Release();
        }
    }

    /// <summary>Guarda a operação e uma cópia do anexo, que o envio lê depois do disco.</summary>
    public async Task EnfileirarAsync(OperacaoPendente operacao, FileResult? anexo)
    {
        if (anexo is not null)
        {
            Directory.CreateDirectory(PastaAnexos);
            var destino = Path.Combine(PastaAnexos, $"{operacao.Id:N}{Path.GetExtension(anexo.FileName).ToLowerInvariant()}");

            await using (var origem = await anexo.OpenReadAsync())
            await using (var copia = File.Create(destino))
            {
                await origem.CopyToAsync(copia);
            }

            operacao.Anexo = destino;
            operacao.AnexoContentType = Helpers.Anexos.ObterContentType(anexo);
        }

        await AlterarAsync(operacoes => operacoes.Add(operacao));
    }

    public Task AtualizarAsync(OperacaoPendente operacao) => AlterarAsync(operacoes =>
    {
        var indice = operacoes.FindIndex(o => o.Id == operacao.Id);
        if (indice >= 0) operacoes[indice] = operacao;
    });

    public async Task RemoverAsync(Guid id)
    {
        string? anexo = null;
        await AlterarAsync(operacoes =>
        {
            anexo = operacoes.FirstOrDefault(o => o.Id == id)?.Anexo;
            operacoes.RemoveAll(o => o.Id == id);
        });

        if (anexo is not null && File.Exists(anexo))
        {
            File.Delete(anexo);
        }
    }

    /// <summary>Descarta tudo ao sair da conta: as operações são do usuário logado.</summary>
    public async Task LimparAsync()
    {
        await AlterarAsync(operacoes => operacoes.Clear());

        if (Directory.Exists(PastaAnexos))
        {
            Directory.Delete(PastaAnexos, recursive: true);
        }
    }

    private async Task AlterarAsync(Action<List<OperacaoPendente>> alteracao)
    {
        await _trava.WaitAsync();
        try
        {
            var operacoes = await CarregarAsync();
            alteracao(operacoes);
            await SalvarAsync(operacoes);
        }
        finally
        {
            _trava.Release();
        }

        WeakReferenceMessenger.Default.Send(new FilaOfflineAlterada());
    }

    private async Task<List<OperacaoPendente>> CarregarAsync()
    {
        if (_operacoes is not null) return _operacoes;

        if (!File.Exists(Arquivo)) return _operacoes = new();

        try
        {
            await using var arquivo = File.OpenRead(Arquivo);
            _operacoes = await JsonSerializer.DeserializeAsync<List<OperacaoPendente>>(arquivo, CacheLocal.Json) ?? new();
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            _operacoes = new();
        }

        return _operacoes;
    }

    private static async Task SalvarAsync(List<OperacaoPendente> operacoes)
    {
        var temporario = Arquivo + ".tmp";
        await using (var arquivo = File.Create(temporario))
        {
            await JsonSerializer.SerializeAsync(arquivo, operacoes, CacheLocal.Json);
        }

        File.Move(temporario, Arquivo, overwrite: true);
    }
}
