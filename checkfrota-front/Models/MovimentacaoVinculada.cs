using System.Text.Json.Serialization;

namespace checkfrota_front.Models
{
    /// <summary>
    /// Lançamento financeiro gerado por um registro da frota (despesa de manutenção ou
    /// abastecimento, receita do frete de uma viagem).
    /// </summary>
    public class MovimentacaoVinculada
    {
        [JsonPropertyName("movimentacao_id")]
        public int MovimentacaoId { get; set; }

        public string Status { get; set; } = "pendente";

        [JsonPropertyName("data_vencimento")]
        public string? DataVencimento { get; set; }

        [JsonPropertyName("data_pagamento")]
        public string? DataPagamento { get; set; }

        public CategoriaResumo? Categoria { get; set; }

        [JsonPropertyName("forma_pagamento")]
        public FormaPagamentoResumo? FormaPagamento { get; set; }

        /// <summary>Nota anexada ao lançamento (abastecimentos e manutenções da frota).</summary>
        public ComprovanteResumo? Comprovante { get; set; }

        [JsonIgnore]
        public bool IsPago => Status == "pago";
    }
}
