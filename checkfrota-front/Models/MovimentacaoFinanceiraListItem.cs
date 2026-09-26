using System.Text.Json.Serialization;

namespace checkfrota_front.Models
{
    public class CategoriaResumo
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Icone { get; set; } = "outros";
    }

    public class FormaPagamentoResumo
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
    }

    public class MovimentacaoFinanceiraListItem
    {
        public int Id { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public decimal Valor { get; set; }

        [JsonPropertyName("data_vencimento")]
        public string? DataVencimento { get; set; }

        [JsonPropertyName("data_pagamento")]
        public string? DataPagamento { get; set; }

        public string Status { get; set; } = "pendente";

        [JsonPropertyName("numero_parcela")]
        public int? NumeroParcela { get; set; }

        [JsonPropertyName("financiamento_id")]
        public int? FinanciamentoId { get; set; }

        public CategoriaResumo? Categoria { get; set; }

        [JsonPropertyName("forma_pagamento")]
        public FormaPagamentoResumo? FormaPagamento { get; set; }

        [JsonPropertyName("criado_em")]
        public string? CriadoEm { get; set; }

        public bool Ativo { get; set; }
    }

    public class MovimentacaoFinanceiraListResponse
    {
        public List<MovimentacaoFinanceiraListItem> Movimentacoes { get; set; } = new();
    }

    public class MovimentacaoFinanceiraResponse
    {
        public MovimentacaoFinanceiraListItem Movimentacao { get; set; } = new();
    }

    public class ResumoFinanceiro
    {
        [JsonPropertyName("saldo_atual")]
        public decimal SaldoAtual { get; set; }

        [JsonPropertyName("saldo_previsto")]
        public decimal SaldoPrevisto { get; set; }

        [JsonPropertyName("total_entradas_pagas")]
        public decimal TotalEntradasPagas { get; set; }

        [JsonPropertyName("total_saidas_pagas")]
        public decimal TotalSaidasPagas { get; set; }

        [JsonPropertyName("total_entradas_pendentes")]
        public decimal TotalEntradasPendentes { get; set; }

        [JsonPropertyName("total_saidas_pendentes")]
        public decimal TotalSaidasPendentes { get; set; }
    }
}
