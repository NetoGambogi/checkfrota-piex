using System.Text.Json.Serialization;

namespace checkfrota_front.Models
{
    /// <summary>
    /// Intervalo de consumo fechado por um abastecimento com tanque cheio: km rodados desde o
    /// tanque cheio anterior e litros colocados nesse meio tempo.
    /// </summary>
    public class ConsumoIntervalo
    {
        [JsonPropertyName("km_rodados")]
        public int KmRodados { get; set; }

        public decimal Litros { get; set; }
        public decimal Valor { get; set; }

        [JsonPropertyName("media_km_l")]
        public decimal MediaKmL { get; set; }

        [JsonPropertyName("custo_por_km")]
        public decimal CustoPorKm { get; set; }
    }

    public class AbastecimentoListItem
    {
        public int Id { get; set; }

        /// <summary>Gerado pelo app ao registrar (vai como client_uuid); casa o item com a fila offline.</summary>
        [JsonPropertyName("client_uuid")]
        public string? ClientUuid { get; set; }

        public VeiculoResumo? Veiculo { get; set; }
        public string? Data { get; set; }
        public int Km { get; set; }
        public decimal Litros { get; set; }

        [JsonPropertyName("valor_total")]
        public decimal ValorTotal { get; set; }

        [JsonPropertyName("preco_litro")]
        public decimal PrecoLitro { get; set; }

        public string Combustivel { get; set; } = "diesel_s10";

        [JsonPropertyName("tanque_cheio")]
        public bool TanqueCheio { get; set; } = true;

        public string? Posto { get; set; }
        public ConsumoIntervalo? Consumo { get; set; }
        public MovimentacaoVinculada? Financeiro { get; set; }

        /// <summary>Foto ou PDF da nota enviado pelo motorista; nulo nos lançados pela frota.</summary>
        public ComprovanteResumo? Comprovante { get; set; }

        [JsonPropertyName("criado_em")]
        public string? CriadoEm { get; set; }

        public bool Ativo { get; set; }
    }

    public class AbastecimentoListResponse
    {
        public List<AbastecimentoListItem> Abastecimentos { get; set; } = new();
    }

    public class AbastecimentoResponse
    {
        public AbastecimentoListItem Abastecimento { get; set; } = new();
    }

    /// <summary>Resumo de consumo e custos de combustível (de um veículo ou da frota).</summary>
    public class ConsumoResumo
    {
        public int Quantidade { get; set; }

        [JsonPropertyName("total_litros")]
        public decimal TotalLitros { get; set; }

        [JsonPropertyName("total_gasto")]
        public decimal TotalGasto { get; set; }

        [JsonPropertyName("preco_medio_litro")]
        public decimal? PrecoMedioLitro { get; set; }

        [JsonPropertyName("custo_medio_abastecimento")]
        public decimal? CustoMedioAbastecimento { get; set; }

        [JsonPropertyName("km_rodados")]
        public int KmRodados { get; set; }

        [JsonPropertyName("media_km_l")]
        public decimal? MediaKmL { get; set; }

        [JsonPropertyName("custo_por_km")]
        public decimal? CustoPorKm { get; set; }
    }

    public class ConsumoVeiculo : ConsumoResumo
    {
        public VeiculoResumo Veiculo { get; set; } = new();
    }

    public class ConsumoResponse
    {
        public List<ConsumoVeiculo> Veiculos { get; set; } = new();
        public ConsumoResumo Geral { get; set; } = new();
    }
}
