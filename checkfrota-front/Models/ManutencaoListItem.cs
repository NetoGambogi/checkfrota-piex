using System.Text.Json.Serialization;

namespace checkfrota_front.Models
{
    public class VeiculoResumo
    {
        public int Id { get; set; }
        public string Placa { get; set; } = string.Empty;
        public string Marca { get; set; } = string.Empty;
        public string Modelo { get; set; } = string.Empty;
    }

    public class ManutencaoListItem
    {
        public int Id { get; set; }
        public string Tipo { get; set; } = "preventiva";
        public string Descricao { get; set; } = string.Empty;
        public string? Data { get; set; }
        public int? Km { get; set; }
        public string? Oficina { get; set; }
        public decimal Valor { get; set; }
        public VeiculoResumo? Veiculo { get; set; }
        public MovimentacaoVinculada? Financeiro { get; set; }

        [JsonPropertyName("criado_em")]
        public string? CriadoEm { get; set; }

        public bool Ativo { get; set; }
    }

    public class ManutencaoListResponse
    {
        public List<ManutencaoListItem> Manutencoes { get; set; } = new();
    }

    public class ManutencaoResponse
    {
        public ManutencaoListItem Manutencao { get; set; } = new();
    }
}
