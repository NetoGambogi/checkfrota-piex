using System.Text.Json.Serialization;

namespace checkfrota_front.Models
{
    public class RotaListItem
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Origem { get; set; } = string.Empty;
        public string Destino { get; set; } = string.Empty;

        [JsonPropertyName("km_aproximado")]
        public int KmAproximado { get; set; }

        public string? Observacoes { get; set; }

        [JsonPropertyName("criado_em")]
        public string? CriadoEm { get; set; }

        public bool Ativo { get; set; }
    }

    public class RotaListResponse
    {
        public List<RotaListItem> Rotas { get; set; } = new();
    }

    public class RotaResponse
    {
        public RotaListItem Rota { get; set; } = new();
    }
}
