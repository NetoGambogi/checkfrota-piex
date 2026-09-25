using System.Text.Json.Serialization;

namespace checkfrota_front.Models
{
    public class CategoriaFinanceiraListItem
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public string? Descricao { get; set; }
        public string Icone { get; set; } = "outros";

        [JsonPropertyName("criado_em")]
        public string? CriadoEm { get; set; }

        public bool Ativo { get; set; }
    }

    public class CategoriaFinanceiraListResponse
    {
        public List<CategoriaFinanceiraListItem> Categorias { get; set; } = new();
    }

    public class CategoriaFinanceiraResponse
    {
        public CategoriaFinanceiraListItem Categoria { get; set; } = new();
    }
}
