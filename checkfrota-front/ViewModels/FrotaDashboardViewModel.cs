using checkfrota_front.Charts;
using checkfrota_front.Models;
using checkfrota_front.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace checkfrota_front.ViewModels;

// Chip de escolha da série no gráfico de consumo: a frota inteira ou um veículo.
public partial class SerieConsumoOpcao : ObservableObject
{
    public required int Indice { get; init; }
    public required string Rotulo { get; init; }

    [ObservableProperty]
    private bool selecionado;
}

public partial class FrotaDashboardViewModel : ObservableObject
{
    private readonly DashboardService _dashboardService;
    private DashboardFrota? _dados;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Is3Meses), nameof(Is6Meses), nameof(Is12Meses), nameof(PeriodoTitulo))]
    private int meses = 6;

    public bool Is3Meses => Meses == 3;
    public bool Is6Meses => Meses == 6;
    public bool Is12Meses => Meses == 12;
    public string PeriodoTitulo => $"Últimos {Meses} meses";

    [ObservableProperty]
    private bool isLoading;

    /// <summary>Recarregando com dados já na tela (ex.: troca de período): aviso discreto no topo.</summary>
    [ObservableProperty]
    private bool isAtualizando;

    private bool _carregando;

    // Período trocado durante um carregamento: recarrega quando ele terminar, para não mostrar o anterior.
    private bool _recarregarAoTerminar;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    public bool HasError => ErrorMessage is not null;

    [ObservableProperty]
    private bool hasData;

    [ObservableProperty] private string mediaGeral = "—";
    [ObservableProperty] private string kmRodados = "—";
    [ObservableProperty] private string custoPorKm = "—";
    [ObservableProperty] private string gastoCombustivel = "—";
    [ObservableProperty] private string gastoManutencao = "—";
    [ObservableProperty] private string receitaFretes = "—";
    [ObservableProperty] private string lucroEstimado = "—";
    [ObservableProperty] private string viagensTexto = string.Empty;

    // Ranking de média (km/l)
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRankingMedia), nameof(HasNoRankingMedia))]
    private IReadOnlyList<ChartCategory> rankingMedia = [];

    public bool HasRankingMedia => RankingMedia.Count > 0;
    public bool HasNoRankingMedia => !HasRankingMedia;

    // Média mês a mês, da frota ou do veículo escolhido no chip
    [ObservableProperty]
    private IReadOnlyList<SerieConsumoOpcao> seriesConsumo = [];

    [ObservableProperty]
    private IReadOnlyList<ChartPoint> consumoMensal = [];

    [ObservableProperty]
    private int consumoSelecionado = -1;

    [ObservableProperty] private string consumoMes = string.Empty;
    [ObservableProperty] private string consumoValor = string.Empty;
    [ObservableProperty] private string consumoDetalhe = string.Empty;

    // Custo por km
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCustoPorKm), nameof(HasNoCustoPorKm))]
    private IReadOnlyList<ChartCategory> rankingCustoPorKm = [];

    public bool HasCustoPorKm => RankingCustoPorKm.Count > 0;
    public bool HasNoCustoPorKm => !HasCustoPorKm;

    [ObservableProperty] private string maiorCustoTexto = string.Empty;

    // Rota mais lucrativa por mês
    [ObservableProperty]
    private IReadOnlyList<ChartPoint> rotasMensal = [];

    [ObservableProperty]
    private int rotaMesSelecionado = -1;

    [ObservableProperty] private string rotaMesTitulo = string.Empty;
    [ObservableProperty] private string melhorRotaMes = string.Empty;
    [ObservableProperty] private string melhorRotaMesDetalhe = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRotasDoMes))]
    private IReadOnlyList<ChartCategory> rotasDoMes = [];

    public bool HasRotasDoMes => RotasDoMes.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMelhorRotaPeriodo))]
    private string? melhorRotaPeriodo;

    public bool HasMelhorRotaPeriodo => MelhorRotaPeriodo is not null;

    // Manutenções por veículo
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasManutencoes), nameof(HasNoManutencoes))]
    private IReadOnlyList<ChartCategory> rankingManutencoes = [];

    public bool HasManutencoes => RankingManutencoes.Count > 0;
    public bool HasNoManutencoes => !HasManutencoes;

    [ObservableProperty] private string maisManutencoesTexto = string.Empty;

    public FrotaDashboardViewModel(DashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    public async Task CarregarAsync()
    {
        if (_carregando)
        {
            _recarregarAoTerminar = true;
            return;
        }

        try
        {
            _carregando = true;
            IsLoading = !HasData;
            IsAtualizando = HasData;
            ErrorMessage = null;

            var dados = await _dashboardService.GetFrotaAsync(Meses);
            if (dados is null)
            {
                ErrorMessage = "Não foi possível carregar o dashboard.";
                return;
            }

            Aplicar(dados);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível carregar o dashboard. Puxe para baixo para tentar de novo.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsLoading = false;
            IsAtualizando = false;
            IsRefreshing = false;
            _carregando = false;
        }

        if (_recarregarAoTerminar)
        {
            _recarregarAoTerminar = false;
            await CarregarAsync();
        }
    }

    [RelayCommand]
    private Task AtualizarAsync() => CarregarAsync();

    [RelayCommand]
    private async Task SelecionarPeriodoAsync(string valor)
    {
        if (!int.TryParse(valor, out var novo) || novo == Meses) return;

        Meses = novo;
        await CarregarAsync();
    }

    [RelayCommand]
    private void SelecionarSerie(SerieConsumoOpcao opcao)
    {
        foreach (var serie in SeriesConsumo)
            serie.Selecionado = serie == opcao;

        AplicarSerieConsumo();
    }

    private void Aplicar(DashboardFrota dados)
    {
        _dados = dados;
        var ind = dados.Indicadores;

        MediaGeral = ind.MediaKmL is decimal media ? KmL(media) : "—";
        KmRodados = $"{ind.KmRodados.ToString("N0", ChartFormat.PtBr)} km";
        CustoPorKm = ind.CustoPorKm is decimal custo ? $"{Moeda(custo)}/km" : "—";
        GastoCombustivel = Moeda(ind.GastoCombustivel);
        GastoManutencao = Moeda(ind.GastoManutencao);
        ReceitaFretes = Moeda(ind.ReceitaFretes);
        LucroEstimado = Moeda(ind.LucroEstimado);
        ViagensTexto = ind.Viagens == 1 ? "em 1 viagem" : $"em {ind.Viagens} viagens";

        RankingMedia = dados.RankingMedia
            .Select(r => new ChartCategory(r.Veiculo.Placa, (double)r.MediaKmL,
                $"{KmL(r.MediaKmL)} · {r.KmRodados.ToString("N0", ChartFormat.PtBr)} km"))
            .ToList();

        // Mantém a série escolhida (frota ou veículo) ao trocar o período, se ela ainda existir.
        var rotuloAnterior = SeriesConsumo.FirstOrDefault(s => s.Selecionado)?.Rotulo;
        SeriesConsumo = dados.ConsumoMensal.Series
            .Select((s, i) => new SerieConsumoOpcao { Indice = i, Rotulo = s.Rotulo })
            .ToList();
        var selecionada = SeriesConsumo.FirstOrDefault(s => s.Rotulo == rotuloAnterior) ?? SeriesConsumo.FirstOrDefault();
        if (selecionada is not null) selecionada.Selecionado = true;
        AplicarSerieConsumo();

        RankingCustoPorKm = dados.CustoPorKm
            .Select(c => new ChartCategory(c.Veiculo.Placa, (double)c.CustoPorKm, $"{Moeda(c.CustoPorKm)}/km"))
            .ToList();
        var maiorCusto = dados.CustoPorKm.FirstOrDefault();
        MaiorCustoTexto = maiorCusto is null
            ? string.Empty
            : $"{maiorCusto.Veiculo.Placa}: {Moeda(maiorCusto.CustoPorKm)}/km em {maiorCusto.KmRodados.ToString("N0", ChartFormat.PtBr)} km "
              + $"(diesel {Moeda(maiorCusto.Combustivel)} + manutenção {Moeda(maiorCusto.Manutencao)})";

        RotasMensal = dados.RotasPorMes
            .Select(m => new ChartPoint(ChartFormat.MesCurto(m.Mes), m.Rotas.Count > 0 ? (double)m.Rotas[0].Lucro : double.NaN))
            .ToList();
        RotaMesSelecionado = UltimoMesComDados(dados.RotasPorMes);
        OnRotaMesSelecionadoChanged(RotaMesSelecionado);

        var melhorPeriodo = dados.RotasRanking.FirstOrDefault();
        MelhorRotaPeriodo = melhorPeriodo is null
            ? null
            : $"{melhorPeriodo.Rota.Nome}: {Moeda(melhorPeriodo.Lucro)} em {melhorPeriodo.Viagens} viagens";

        RankingManutencoes = dados.ManutencoesPorVeiculo
            .Select(m => new ChartCategory(m.Veiculo.Placa, m.Quantidade, $"{m.Quantidade} · {Moeda(m.ValorTotal)}"))
            .ToList();
        var mais = dados.ManutencoesPorVeiculo.FirstOrDefault();
        MaisManutencoesTexto = mais is null
            ? string.Empty
            : $"{mais.Veiculo.Placa}: {Plural(mais.Quantidade, "manutenção", "manutenções")} "
              + $"({Plural(mais.Corretivas, "corretiva", "corretivas")}), {Moeda(mais.ValorTotal)}";

        HasData = true;
    }

    private void AplicarSerieConsumo()
    {
        if (_dados is null) return;

        var indice = SeriesConsumo.FirstOrDefault(s => s.Selecionado)?.Indice ?? 0;
        var serie = _dados.ConsumoMensal.Series.ElementAtOrDefault(indice);
        var meses = _dados.ConsumoMensal.Meses;

        ConsumoMensal = meses
            .Select((mes, i) => new ChartPoint(ChartFormat.MesCurto(mes), serie?.Valores.ElementAtOrDefault(i) is decimal v ? (double)v : double.NaN))
            .ToList();

        // Começa no último mês com medição.
        var ultimoComMedicao = ConsumoMensal.Select((p, i) => double.IsNaN(p.Valor) ? -1 : i).DefaultIfEmpty(-1).Max();
        ConsumoSelecionado = ultimoComMedicao >= 0 ? ultimoComMedicao : ConsumoMensal.Count - 1;
        OnConsumoSelecionadoChanged(ConsumoSelecionado);
    }

    partial void OnConsumoSelecionadoChanged(int value)
    {
        if (_dados is null || value < 0 || value >= ConsumoMensal.Count) return;

        var nome = SeriesConsumo.FirstOrDefault(s => s.Selecionado)?.Rotulo ?? "Frota";
        var atual = ConsumoMensal[value].Valor;
        ConsumoMes = $"{nome} em {ChartFormat.MesLongo(_dados.ConsumoMensal.Meses[value])}";
        ConsumoValor = double.IsNaN(atual) ? "Sem medição" : KmL((decimal)atual);

        var anterior = value > 0 ? ConsumoMensal[value - 1].Valor : double.NaN;
        ConsumoDetalhe = double.IsNaN(atual)
            ? "A média depende de dois abastecimentos com tanque cheio no mês."
            : double.IsNaN(anterior)
                ? "Sem medição no mês anterior para comparar."
                : $"{(atual >= anterior ? "+" : "−")}{Math.Abs(atual - anterior).ToString("0.00", ChartFormat.PtBr)} km/l em relação ao mês anterior";
    }

    partial void OnRotaMesSelecionadoChanged(int value)
    {
        if (_dados is null || value < 0 || value >= _dados.RotasPorMes.Count) return;

        var mes = _dados.RotasPorMes[value];
        RotaMesTitulo = Capitalizar(ChartFormat.MesLongo(mes.Mes));
        RotasDoMes = mes.Rotas
            .Select(r => new ChartCategory(r.Rota.Nome, Math.Max(0, (double)r.Lucro), Moeda(r.Lucro)))
            .ToList();

        var melhor = mes.Rotas.FirstOrDefault();
        MelhorRotaMes = melhor?.Rota.Nome ?? "Nenhuma viagem com frete";
        MelhorRotaMesDetalhe = melhor is null
            ? string.Empty
            : $"{Moeda(melhor.Lucro)} de lucro estimado em {Plural(melhor.Viagens, "viagem", "viagens")} (frete {Moeda(melhor.Receita)} − custo {Moeda(melhor.CustoEstimado)})";
    }

    private static int UltimoMesComDados(List<RotasDoMes> meses)
    {
        for (var i = meses.Count - 1; i >= 0; i--)
            if (meses[i].Rotas.Count > 0) return i;

        return meses.Count - 1;
    }

    private static string KmL(decimal valor) => $"{valor.ToString("N2", ChartFormat.PtBr)} km/l";

    private static string Moeda(decimal valor) => valor.ToString("C2", ChartFormat.PtBr);

    private static string Plural(int quantidade, string singular, string plural) =>
        $"{quantidade} {(quantidade == 1 ? singular : plural)}";

    private static string Capitalizar(string texto) =>
        texto.Length == 0 ? texto : char.ToUpper(texto[0], ChartFormat.PtBr) + texto[1..];
}
