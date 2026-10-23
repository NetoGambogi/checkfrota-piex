using System.Text.Json.Serialization;

namespace checkfrota_front.Models
{
    public class OcorrenciaViagemResumo
    {
        public int Id { get; set; }
        public RotaResumo? Rota { get; set; }

        [JsonPropertyName("data_saida")]
        public string? DataSaida { get; set; }

        public string Status { get; set; } = "em_andamento";
    }

    public class PessoaResumo
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class OcorrenciaFoto
    {
        public string Url { get; set; } = string.Empty;

        /// <summary>Versão limitada a 1600px, para exibir no app.</summary>
        [JsonPropertyName("url_exibicao")]
        public string UrlExibicao { get; set; } = string.Empty;
    }

    /// <summary>Problema relatado pelo motorista durante uma viagem.</summary>
    public class OcorrenciaItem
    {
        public int Id { get; set; }
        public OcorrenciaViagemResumo? Viagem { get; set; }
        public VeiculoResumo? Veiculo { get; set; }
        public PessoaResumo? Motorista { get; set; }

        public string Tipo { get; set; } = "outro";

        [JsonPropertyName("tipo_label")]
        public string TipoLabel { get; set; } = string.Empty;

        /// <summary>"baixa", "media" ou "alta".</summary>
        public string Gravidade { get; set; } = "baixa";

        [JsonPropertyName("gravidade_label")]
        public string GravidadeLabel { get; set; } = string.Empty;

        public int Km { get; set; }
        public string Descricao { get; set; } = string.Empty;
        public OcorrenciaFoto? Foto { get; set; }

        /// <summary>"aberta" ou "resolvida".</summary>
        public string Status { get; set; } = "aberta";

        [JsonPropertyName("resolvida_em")]
        public string? ResolvidaEm { get; set; }

        [JsonPropertyName("resolvida_por")]
        public PessoaResumo? ResolvidaPor { get; set; }

        [JsonPropertyName("observacao_resolucao")]
        public string? ObservacaoResolucao { get; set; }

        [JsonPropertyName("criado_em")]
        public string? CriadoEm { get; set; }
    }

    public class OcorrenciaListResponse
    {
        public List<OcorrenciaItem> Ocorrencias { get; set; } = new();

        /// <summary>Total de ocorrências abertas (só na listagem da gestão).</summary>
        public int Abertas { get; set; }
    }

    public class OcorrenciaResponse
    {
        public OcorrenciaItem Ocorrencia { get; set; } = new();
    }
}
