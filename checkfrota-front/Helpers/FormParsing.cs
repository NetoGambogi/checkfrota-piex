using System.Globalization;

namespace checkfrota_front.Helpers;

/// <summary>Conversão dos campos digitados nos formulários da frota.</summary>
public static class FormParsing
{
    /// <summary>Valor em reais maior que zero; aceita vírgula ou ponto como separador decimal.</summary>
    public static bool TryParseValor(string? texto, out decimal valor) => TryParseDecimal(texto, out valor) && valor > 0;

    /// <summary>Número decimal (ex: litros); aceita vírgula ou ponto como separador decimal.</summary>
    public static bool TryParseDecimal(string? texto, out decimal valor)
    {
        valor = 0;
        if (string.IsNullOrWhiteSpace(texto)) return false;

        return decimal.TryParse(texto.Trim().Replace(",", "."), NumberStyles.Number, CultureInfo.InvariantCulture, out valor);
    }

    /// <summary>Quilometragem inteira e não negativa; aceita ponto como separador de milhar (ex: 120.000).</summary>
    public static bool TryParseKm(string? texto, out int km)
    {
        km = 0;
        return !string.IsNullOrWhiteSpace(texto) && int.TryParse(texto.Trim().Replace(".", ""), out km) && km >= 0;
    }

    /// <summary>Como <see cref="TryParseKm"/>, mas campo vazio é válido e resulta em null.</summary>
    public static bool TryParseKmOpcional(string? texto, out int? km)
    {
        km = null;
        if (string.IsNullOrWhiteSpace(texto)) return true;

        if (!TryParseKm(texto, out var valor)) return false;

        km = valor;
        return true;
    }
}
