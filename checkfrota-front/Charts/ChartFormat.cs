using System.Globalization;

namespace checkfrota_front.Charts;

public static class ChartFormat
{
    public static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Moeda(double valor) => valor.ToString("C2", PtBr);

    // Valores de eixo: "R$ 950", "R$ 1,2 mil", "R$ 3 mi".
    public static string MoedaCompacta(double valor)
    {
        var sinal = valor < 0 ? "-" : string.Empty;
        var abs = Math.Abs(valor);

        return abs switch
        {
            >= 1_000_000 => $"{sinal}R$ {(abs / 1_000_000).ToString("0.#", PtBr)} mi",
            >= 1_000 => $"{sinal}R$ {(abs / 1_000).ToString("0.#", PtBr)} mil",
            _ => $"{sinal}R$ {abs.ToString("0", PtBr)}",
        };
    }

    // "2026-10" -> "out/26"
    public static string MesCurto(string anoMes) =>
        DateTime.TryParseExact(anoMes, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
            ? data.ToString("MMM/yy", PtBr).Replace(".", string.Empty)
            : anoMes;

    // "2026-10" -> "outubro de 2026"
    public static string MesLongo(string anoMes) =>
        DateTime.TryParseExact(anoMes, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
            ? data.ToString("MMMM 'de' yyyy", PtBr)
            : anoMes;

    // Escala "redonda" (0 / 500 / 1.000...) que cobre [minimo, maximo] com cerca de `divisoes` intervalos.
    public static (double Min, double Max, double Passo) EscalaRedonda(double minimo, double maximo, int divisoes = 4)
    {
        if (maximo <= minimo)
            maximo = minimo + (minimo == 0 ? 100 : Math.Abs(minimo));

        var passo = NumeroRedondo((maximo - minimo) / divisoes);
        return (Math.Floor(minimo / passo) * passo, Math.Ceiling(maximo / passo) * passo, passo);
    }

    private static double NumeroRedondo(double valor)
    {
        var expoente = Math.Floor(Math.Log10(valor));
        var fracao = valor / Math.Pow(10, expoente);
        var redondo = fracao switch
        {
            <= 1 => 1,
            <= 2 => 2,
            <= 2.5 => 2.5,
            <= 5 => 5,
            _ => 10,
        };
        return redondo * Math.Pow(10, expoente);
    }
}
