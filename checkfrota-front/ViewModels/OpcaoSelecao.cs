using System.Globalization;
using System.Text;

namespace checkfrota_front.ViewModels;

/// <summary>
/// Item do <see cref="Views.Shared.SelecaoDialog"/>. <see cref="Valor"/> é o que a tela recebe de
/// volta (ex.: o veículo); null numa opção como "Todos os caminhões".
/// </summary>
public class OpcaoSelecao
{
    public required string Titulo { get; init; }
    public string? Subtitulo { get; init; }
    public object? Valor { get; init; }
    public bool Selecionada { get; init; }

    public bool HasSubtitulo => !string.IsNullOrEmpty(Subtitulo);

    /// <summary>Busca sem diferenciar maiúsculas, acentos, hífens e espaços (ex.: "abc1d" acha "ABC-1D23").</summary>
    public bool Corresponde(string termo)
    {
        var busca = Normalizar(termo);
        return busca.Length == 0 || Normalizar($"{Titulo} {Subtitulo}").Contains(busca, StringComparison.Ordinal);
    }

    private static string Normalizar(string texto)
    {
        var resultado = new StringBuilder(texto.Length);
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
        {
            if (char.IsLetterOrDigit(c) && CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                resultado.Append(char.ToLowerInvariant(c));
        }

        return resultado.ToString();
    }
}
