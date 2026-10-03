using System.Text.Json.Serialization;

namespace checkfrota_front.Models
{
    public class VeiculoListItem
    {
        public int Id { get; set; }
        public string Placa { get; set; } = string.Empty;
        public string Tipo { get; set; } = "caminhao";
        public string Marca { get; set; } = string.Empty;
        public string Modelo { get; set; } = string.Empty;
        public int? Ano { get; set; }
        public string? Renavam { get; set; }

        [JsonPropertyName("km_atual")]
        public int KmAtual { get; set; }

        public string? Observacoes { get; set; }

        [JsonPropertyName("total_manutencoes")]
        public decimal? TotalManutencoes { get; set; }

        [JsonPropertyName("criado_em")]
        public string? CriadoEm { get; set; }

        public bool Ativo { get; set; }

        /// <summary>Texto exibido nos seletores de veículo, ex: "ABC1D23 · Volvo FH 540".</summary>
        [JsonIgnore]
        public string Rotulo => $"{Placa} · {Marca} {Modelo}";
    }

    public class VeiculoListResponse
    {
        public List<VeiculoListItem> Veiculos { get; set; } = new();
    }

    public class VeiculoResponse
    {
        public VeiculoListItem Veiculo { get; set; } = new();
    }
}
