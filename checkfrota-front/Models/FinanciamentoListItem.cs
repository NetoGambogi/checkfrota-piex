using System.Text.Json.Serialization;

namespace checkfrota_front.Models
{
    public class FinanciamentoListItem
    {
        public int Id { get; set; }
        public string Descricao { get; set; } = string.Empty;
        public CategoriaResumo? Categoria { get; set; }

        [JsonPropertyName("forma_pagamento")]
        public FormaPagamentoResumo? FormaPagamento { get; set; }

        [JsonPropertyName("valor_parcela")]
        public decimal ValorParcela { get; set; }

        [JsonPropertyName("quantidade_parcelas")]
        public int QuantidadeParcelas { get; set; }

        [JsonPropertyName("valor_total")]
        public decimal ValorTotal { get; set; }

        [JsonPropertyName("dia_vencimento")]
        public int DiaVencimento { get; set; }

        [JsonPropertyName("data_inicio")]
        public string? DataInicio { get; set; }

        [JsonPropertyName("parcelas_pagas")]
        public int? ParcelasPagas { get; set; }

        [JsonPropertyName("criado_em")]
        public string? CriadoEm { get; set; }

        public bool Ativo { get; set; }
    }

    public class FinanciamentoListResponse
    {
        public List<FinanciamentoListItem> Financiamentos { get; set; } = new();
    }

    public class FinanciamentoResponse
    {
        public FinanciamentoListItem Financiamento { get; set; } = new();
    }

    public class FinanciamentoDetalheResponse
    {
        public FinanciamentoListItem Financiamento { get; set; } = new();
        public List<MovimentacaoFinanceiraListItem> Parcelas { get; set; } = new();
    }
}
