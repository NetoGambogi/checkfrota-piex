using System.Text.Json.Serialization;

namespace checkfrota_front.Models
{
    public class DashboardFinanceiro
    {
        public string Referencia { get; set; } = string.Empty;
        public int Meses { get; set; }
        public IndicadoresFinanceiros Indicadores { get; set; } = new();
        public List<HistoricoMensal> Historico { get; set; } = new();
        public List<PrevisaoMensal> Previsao { get; set; } = new();

        [JsonPropertyName("previsao_historica")]
        public List<PrevisaoMensal> PrevisaoHistorica { get; set; } = new();

        [JsonPropertyName("base_historica_meses")]
        public int BaseHistoricaMeses { get; set; }

        [JsonPropertyName("despesas_por_categoria")]
        public List<TotalPorCategoria> DespesasPorCategoria { get; set; } = new();

        [JsonPropertyName("receitas_por_categoria")]
        public List<TotalPorCategoria> ReceitasPorCategoria { get; set; } = new();

        [JsonPropertyName("proximos_vencimentos")]
        public List<VencimentoResumo> ProximosVencimentos { get; set; } = new();
    }

    public class IndicadoresFinanceiros
    {
        [JsonPropertyName("saldo_atual")]
        public decimal SaldoAtual { get; set; }

        [JsonPropertyName("saldo_previsto")]
        public decimal SaldoPrevisto { get; set; }

        [JsonPropertyName("entradas_mes")]
        public decimal EntradasMes { get; set; }

        [JsonPropertyName("saidas_mes")]
        public decimal SaidasMes { get; set; }

        [JsonPropertyName("a_receber")]
        public decimal AReceber { get; set; }

        [JsonPropertyName("a_pagar")]
        public decimal APagar { get; set; }

        [JsonPropertyName("vencidos_valor")]
        public decimal VencidosValor { get; set; }

        [JsonPropertyName("vencidos_quantidade")]
        public int VencidosQuantidade { get; set; }

        [JsonPropertyName("a_receber_vencidos_valor")]
        public decimal AReceberVencidosValor { get; set; }
    }

    public class HistoricoMensal
    {
        public string Mes { get; set; } = string.Empty;
        public decimal Entradas { get; set; }
        public decimal Saidas { get; set; }
    }

    public class PrevisaoMensal
    {
        public string Mes { get; set; } = string.Empty;
        public decimal Entradas { get; set; }
        public decimal Saidas { get; set; }
        public decimal Saldo { get; set; }
    }

    public class TotalPorCategoria
    {
        public string Categoria { get; set; } = string.Empty;
        public decimal Total { get; set; }
    }

    public class VencimentoResumo
    {
        public int Id { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public decimal Valor { get; set; }

        [JsonPropertyName("data_vencimento")]
        public string DataVencimento { get; set; } = string.Empty;

        public bool Vencido { get; set; }
    }
}
