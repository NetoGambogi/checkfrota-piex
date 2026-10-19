using System.Text.Json.Serialization;

namespace checkfrota_front.Models
{
    public class NotificacaoItem
    {
        public string Id { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string Mensagem { get; set; } = string.Empty;

        // Tela aberta ao tocar: "movimentacoes", "viagens", "usuarios" ou null.
        public string? Destino { get; set; }

        public bool Lida { get; set; }

        [JsonPropertyName("criada_em")]
        public string? CriadaEm { get; set; }
    }

    public class NotificacaoListResponse
    {
        [JsonPropertyName("nao_lidas")]
        public int NaoLidas { get; set; }

        public List<NotificacaoItem> Notificacoes { get; set; } = new();
    }

    public class NaoLidasResponse
    {
        [JsonPropertyName("nao_lidas")]
        public int NaoLidas { get; set; }
    }
}