using System.Globalization;

namespace checkfrota_front.Helpers;

/// <summary>
/// Limites dos valores digitados nos formulários da frota. São os mesmos validados pela API
/// (Rota::KM_MAXIMO, Veiculo::KM_MAXIMO_ACIMA_DO_ATUAL, Abastecimento::PRECO_LITRO_*); aqui eles só
/// antecipam a mensagem antes do envio.
/// </summary>
public static class LimitesFrota
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>Distância máxima de uma rota; também limita o km de chegada em relação ao de saída.</summary>
    public const int KmMaximoRota = 7000;

    /// <summary>Quanto uma leitura do hodômetro pode passar do km atual do veículo.</summary>
    public const int KmMaximoAcimaDoAtual = 2000;

    public const decimal PrecoLitroMinimo = 3m;
    public const decimal PrecoLitroMaximo = 15m;

    /// <summary>Mensagem de erro se a leitura do hodômetro passar do km atual + o limite; senão null.</summary>
    public static string? ValidarKmHodometro(int? km, int kmAtual)
    {
        if (km is not int valor) return null;

        var limite = kmAtual + KmMaximoAcimaDoAtual;
        return valor > limite
            ? $"O km deve ser no máximo {Km(limite)}: até {Km(KmMaximoAcimaDoAtual)} km acima do km atual do veículo ({Km(kmAtual)})."
            : null;
    }

    /// <summary>Mensagem de erro se o km de chegada passar do km de saída + a distância máxima de uma rota; senão null.</summary>
    public static string? ValidarKmChegada(int? kmChegada, int kmSaida)
    {
        if (kmChegada is not int valor) return null;

        var limite = kmSaida + KmMaximoRota;
        return valor > limite
            ? $"O km de chegada deve ser no máximo {Km(limite)}: até {Km(KmMaximoRota)} km acima do km de saída ({Km(kmSaida)})."
            : null;
    }

    /// <summary>Mensagem de erro se o preço por litro (valor total / litros) estiver fora da faixa aceita; senão null.</summary>
    public static string? ValidarPrecoPorLitro(decimal litros, decimal valorTotal)
    {
        if (litros <= 0) return null;

        var precoPorLitro = Math.Round(valorTotal / litros, 3);
        return precoPorLitro < PrecoLitroMinimo || precoPorLitro > PrecoLitroMaximo
            ? $"O preço por litro ficou em {precoPorLitro.ToString("C3", PtBr)}; ele deve estar entre {PrecoLitroMinimo.ToString("C2", PtBr)} e {PrecoLitroMaximo.ToString("C2", PtBr)}. Confira o valor total e os litros."
            : null;
    }

    private static string Km(int km) => km.ToString("N0", PtBr);
}
