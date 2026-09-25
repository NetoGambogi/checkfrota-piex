using System.Text.Json.Serialization;

namespace checkfrota_front.Models
{
    public class FormaPagamentoListItem
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;

        [JsonPropertyName("criado_em")]
        public string? CriadoEm { get; set; }

        public bool Ativo { get; set; }
    }

    public class FormaPagamentoListResponse
    {
        public List<FormaPagamentoListItem> Formas { get; set; } = new();
    }

    public class FormaPagamentoResponse
    {
        public FormaPagamentoListItem Forma { get; set; } = new();
    }
}
