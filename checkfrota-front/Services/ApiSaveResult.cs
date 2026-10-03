using System.Net.Http.Json;
using System.Text.Json;

namespace checkfrota_front.Services;

/// <summary>
/// Resultado de um cadastro/edição na API: o item salvo ou a mensagem de erro devolvida
/// (primeira mensagem de validação do Laravel, ex: "Já existe um veículo com essa placa").
/// </summary>
public record ApiSaveResult<T>(T? Item, string? Erro)
{
    public bool Sucesso => Item is not null;

    public static async Task<ApiSaveResult<T>> FromResponseAsync<TResponse>(HttpResponseMessage response, Func<TResponse, T?> selector)
    {
        if (response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<TResponse>();
            return new ApiSaveResult<T>(body is null ? default : selector(body), null);
        }

        return new ApiSaveResult<T>(default, await ReadErrorMessageAsync(response));
    }

    private static async Task<string?> ReadErrorMessageAsync(HttpResponseMessage response)
    {
        try
        {
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = document.RootElement;

            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                foreach (var campo in errors.EnumerateObject())
                {
                    if (campo.Value.ValueKind == JsonValueKind.Array && campo.Value.GetArrayLength() > 0)
                    {
                        return campo.Value[0].GetString();
                    }
                }
            }

            return root.TryGetProperty("message", out var message) ? message.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
