using System.Text.Json;

namespace checkfrota_front.Services;

/// <summary>
/// Última resposta das consultas do motorista, gravada em JSON no aparelho: sem rede, as telas
/// mostram o que foi carregado da última vez em vez de erro.
/// </summary>
public class CacheLocal
{
    // Mesmo formato do GetFromJsonAsync: as propriedades sem [JsonPropertyName] vão em camelCase.
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly string Pasta = Path.Combine(FileSystem.AppDataDirectory, "cache");

    public async Task SalvarAsync<T>(string chave, T valor)
    {
        Directory.CreateDirectory(Pasta);

        // Grava num temporário e troca: um app fechado no meio da escrita não corrompe o cache.
        var temporario = Path.Combine(Pasta, $"{chave}.{Guid.NewGuid():N}.tmp");
        await using (var arquivo = File.Create(temporario))
        {
            await JsonSerializer.SerializeAsync(arquivo, valor, Json);
        }

        File.Move(temporario, Caminho(chave), overwrite: true);
    }

    public async Task<T?> LerAsync<T>(string chave)
    {
        if (!File.Exists(Caminho(chave))) return default;

        try
        {
            await using var arquivo = File.OpenRead(Caminho(chave));
            return await JsonSerializer.DeserializeAsync<T>(arquivo, Json);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    /// <summary>Apaga tudo ao sair da conta: o cache é do usuário logado.</summary>
    public void Limpar()
    {
        if (Directory.Exists(Pasta))
        {
            Directory.Delete(Pasta, recursive: true);
        }
    }

    private static string Caminho(string chave) => Path.Combine(Pasta, $"{chave}.json");
}
