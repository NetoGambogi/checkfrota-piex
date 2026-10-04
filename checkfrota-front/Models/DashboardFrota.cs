using System.Text.Json.Serialization;

namespace checkfrota_front.Models
{
    public class DashboardFrota
    {
        public string Referencia { get; set; } = string.Empty;
        public int Meses { get; set; }
        public IndicadoresFrota Indicadores { get; set; } = new();

        [JsonPropertyName("ranking_media")]
        public List<RankingMediaVeiculo> RankingMedia { get; set; } = new();

        [JsonPropertyName("consumo_mensal")]
        public ConsumoMensalFrota ConsumoMensal { get; set; } = new();

        [JsonPropertyName("custo_por_km")]
        public List<CustoPorKmVeiculo> CustoPorKm { get; set; } = new();

        [JsonPropertyName("rotas_por_mes")]
        public List<RotasDoMes> RotasPorMes { get; set; } = new();

        [JsonPropertyName("rotas_ranking")]
        public List<ResultadoRota> RotasRanking { get; set; } = new();

        [JsonPropertyName("manutencoes_por_veiculo")]
        public List<ManutencoesVeiculo> ManutencoesPorVeiculo { get; set; } = new();
    }

    public class IndicadoresFrota
    {
        [JsonPropertyName("media_km_l")]
        public decimal? MediaKmL { get; set; }

        [JsonPropertyName("km_rodados")]
        public int KmRodados { get; set; }

        [JsonPropertyName("custo_por_km")]
        public decimal? CustoPorKm { get; set; }

        [JsonPropertyName("gasto_combustivel")]
        public decimal GastoCombustivel { get; set; }

        [JsonPropertyName("gasto_manutencao")]
        public decimal GastoManutencao { get; set; }

        [JsonPropertyName("receita_fretes")]
        public decimal ReceitaFretes { get; set; }

        public int Viagens { get; set; }

        [JsonPropertyName("lucro_estimado")]
        public decimal LucroEstimado { get; set; }
    }

    public class RankingMediaVeiculo
    {
        public VeiculoResumo Veiculo { get; set; } = new();

        [JsonPropertyName("media_km_l")]
        public decimal MediaKmL { get; set; }

        [JsonPropertyName("km_rodados")]
        public int KmRodados { get; set; }

        public decimal Litros { get; set; }
    }

    public class ConsumoMensalFrota
    {
        public List<string> Meses { get; set; } = new();
        public List<SerieConsumo> Series { get; set; } = new();
    }

    /// <summary>Média km/l mês a mês da frota (Veiculo nulo) ou de um veículo; null nos meses sem medição.</summary>
    public class SerieConsumo
    {
        public VeiculoResumo? Veiculo { get; set; }
        public string Rotulo { get; set; } = string.Empty;
        public List<decimal?> Valores { get; set; } = new();
    }

    public class CustoPorKmVeiculo
    {
        public VeiculoResumo Veiculo { get; set; } = new();

        [JsonPropertyName("km_rodados")]
        public int KmRodados { get; set; }

        public decimal Combustivel { get; set; }
        public decimal Manutencao { get; set; }

        [JsonPropertyName("custo_total")]
        public decimal CustoTotal { get; set; }

        [JsonPropertyName("custo_por_km")]
        public decimal CustoPorKm { get; set; }
    }

    public class RotasDoMes
    {
        public string Mes { get; set; } = string.Empty;
        public List<ResultadoRota> Rotas { get; set; } = new();
    }

    public class ResultadoRota
    {
        public RotaResumo Rota { get; set; } = new();
        public int Viagens { get; set; }
        public decimal Receita { get; set; }

        [JsonPropertyName("custo_estimado")]
        public decimal CustoEstimado { get; set; }

        public decimal Lucro { get; set; }
    }

    public class ManutencoesVeiculo
    {
        public VeiculoResumo Veiculo { get; set; } = new();
        public int Quantidade { get; set; }
        public int Preventivas { get; set; }
        public int Corretivas { get; set; }

        [JsonPropertyName("valor_total")]
        public decimal ValorTotal { get; set; }
    }
}
