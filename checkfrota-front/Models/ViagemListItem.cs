using System.Text.Json.Serialization;

namespace checkfrota_front.Models
{
    public class RotaResumo
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Origem { get; set; } = string.Empty;
        public string Destino { get; set; } = string.Empty;

        [JsonPropertyName("km_aproximado")]
        public int KmAproximado { get; set; }
    }

    public class ViagemListItem
    {
        public int Id { get; set; }
        public RotaResumo? Rota { get; set; }
        public VeiculoResumo? Veiculo { get; set; }

        [JsonPropertyName("data_saida")]
        public string? DataSaida { get; set; }

        [JsonPropertyName("data_chegada")]
        public string? DataChegada { get; set; }

        /// <summary>"em_andamento" ou "concluida".</summary>
        public string Status { get; set; } = "em_andamento";

        [JsonPropertyName("km_saida")]
        public int? KmSaida { get; set; }

        [JsonPropertyName("km_chegada")]
        public int? KmChegada { get; set; }

        [JsonPropertyName("km_percorrido")]
        public int? KmPercorrido { get; set; }

        [JsonPropertyName("km_estimado")]
        public int? KmEstimado { get; set; }

        public string? Cliente { get; set; }

        [JsonPropertyName("valor_frete")]
        public decimal? ValorFrete { get; set; }

        [JsonPropertyName("frete_por_km")]
        public decimal? FretePorKm { get; set; }

        public string? Observacoes { get; set; }

        public MovimentacaoVinculada? Financeiro { get; set; }

        [JsonPropertyName("criado_em")]
        public string? CriadoEm { get; set; }

        public bool Ativo { get; set; }
    }

    public class ViagemListResponse
    {
        public List<ViagemListItem> Viagens { get; set; } = new();
    }

    public class ViagemResponse
    {
        public ViagemListItem Viagem { get; set; } = new();
    }
}
