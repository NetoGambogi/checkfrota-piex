using checkfrota_front.Charts;
using checkfrota_front.Helpers;
using checkfrota_front.Models;
using checkfrota_front.Services;
using checkfrota_front.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;

namespace checkfrota_front.ViewModels;

public record VencimentoItem(string Descricao, string DataTexto, string ValorTexto, string Icone, bool IsEntrada, bool Vencido);

public partial class FinanceiroDashboardViewModel : ObservableObject
{
    private readonly DashboardService _dashboardService;
    private DashboardFinanceiro? _dados;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Is3Meses), nameof(Is6Meses), nameof(Is12Meses), nameof(HistoricoTitulo), nameof(CategoriasTitulo), nameof(ReceitasTitulo))]
    private int meses = 6;

    public bool Is3Meses => Meses == 3;
    public bool Is6Meses => Meses == 6;
    public bool Is12Meses => Meses == 12;
    public string HistoricoTitulo => $"Realizado nos últimos {Meses} meses (pela data de pagamento)";
    public string CategoriasTitulo => $"Despesas pagas nos últimos {Meses} meses";
    public string ReceitasTitulo => $"Receitas recebidas nos últimos {Meses} meses";

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;

    public bool HasError => ErrorMessage is not null;

    [ObservableProperty]
    private bool hasData;

    [ObservableProperty] private string saldoAtual = "—";
    [ObservableProperty] private string saldoPrevisto = "—";
    [ObservableProperty] private string entradasMes = "—";
    [ObservableProperty] private string saidasMes = "—";
    [ObservableProperty] private string aReceber = "—";
    [ObservableProperty] private string aPagar = "—";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasVencidos))]
    private string? vencidosTexto;

    public bool HasVencidos => VencidosTexto is not null;

    [ObservableProperty]
    private IReadOnlyList<ChartBarGroup> historico = [];

    [ObservableProperty]
    private int historicoSelecionado = -1;

    [ObservableProperty] private string historicoMes = string.Empty;
    [ObservableProperty] private string historicoEntradas = string.Empty;
    [ObservableProperty] private string historicoSaidas = string.Empty;
    [ObservableProperty] private string historicoResultado = string.Empty;

    // "lancamentos": saldo + pendentes já lançados; "historico": tendência dos meses passados.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPrevisaoLancamentos), nameof(IsPrevisaoHistorica), nameof(PrevisaoSubtitulo))]
    private string modoPrevisao = "lancamentos";

    public bool IsPrevisaoLancamentos => ModoPrevisao == "lancamentos";
    public bool IsPrevisaoHistorica => ModoPrevisao == "historico";

    public string PrevisaoSubtitulo => IsPrevisaoHistorica
        ? $"Por categoria, o maior valor entre o já lançado e a média dos últimos {_dados?.BaseHistoricaMeses ?? 6} meses completos"
        : "Saldo atual somado aos lançamentos pendentes, mês a mês (próximos 6 meses)";

    [ObservableProperty]
    private IReadOnlyList<ChartPoint> previsao = [];

    [ObservableProperty]
    private int previsaoSelecionada = -1;

    [ObservableProperty] private string previsaoMes = string.Empty;
    [ObservableProperty] private string previsaoSaldo = string.Empty;
    [ObservableProperty] private string previsaoDetalhe = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAvisoSaldoNegativo))]
    private string? avisoSaldoNegativo;

    public bool HasAvisoSaldoNegativo => AvisoSaldoNegativo is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCategorias), nameof(HasNoCategorias))]
    private IReadOnlyList<ChartCategory> categorias = [];

    public bool HasCategorias => Categorias.Count > 0;
    public bool HasNoCategorias => !HasCategorias;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReceitas), nameof(HasNoReceitas))]
    private IReadOnlyList<ChartCategory> receitas = [];

    public bool HasReceitas => Receitas.Count > 0;
    public bool HasNoReceitas => !HasReceitas;

    [ObservableProperty]
    private string maiorReceita = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasVencimentos), nameof(HasNoVencimentos))]
    private IReadOnlyList<VencimentoItem> proximosVencimentos = [];

    public bool HasVencimentos => ProximosVencimentos.Count > 0;
    public bool HasNoVencimentos => !HasVencimentos;

    public FinanceiroDashboardViewModel(DashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    public async Task CarregarAsync()
    {
        if (IsLoading) return;

        try
        {
            IsLoading = !HasData;
            ErrorMessage = null;

            var dados = await _dashboardService.GetFinanceiroAsync(Meses);
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
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private Task AtualizarAsync() => CarregarAsync();

    [RelayCommand]
    private void SelecionarModoPrevisao(string modo)
    {
        if (modo == ModoPrevisao) return;

        ModoPrevisao = modo;
        AplicarPrevisao();
    }

    [RelayCommand]
    private async Task SelecionarPeriodoAsync(string valor)
    {
        if (!int.TryParse(valor, out var novo) || novo == Meses) return;

        Meses = novo;
        await CarregarAsync();
    }

    // Pendentes desde um ano atrás até o fim da janela da previsão (inclui os vencidos).
    [RelayCommand]
    private Task AbrirPendentesAsync()
    {
        var hoje = DateTime.Today;
        var fimPrevisao = new DateTime(hoje.Year, hoje.Month, 1).AddMonths(6).AddDays(-1);
        return AbrirLancamentosAsync(new FiltroLancamentos("todos", "pendente", hoje.AddYears(-1), fimPrevisao));
    }

    // Mesmo critério do alerta: saídas pendentes com vencimento antes de hoje.
    [RelayCommand]
    private Task AbrirVencidosAsync()
    {
        var hoje = DateTime.Today;
        return AbrirLancamentosAsync(new FiltroLancamentos("saida", "pendente", hoje.AddYears(-1), hoje.AddDays(-1)));
    }

    private static Task AbrirLancamentosAsync(FiltroLancamentos filtro) =>
        Shell.Current.GoToAsync(nameof(MovimentacaoFinanceiraManagementPage), new Dictionary<string, object>
        {
            ["filtro"] = filtro,
        });

    private void Aplicar(DashboardFinanceiro dados)
    {
        _dados = dados;
        var ind = dados.Indicadores;

        SaldoAtual = Moeda(ind.SaldoAtual);
        SaldoPrevisto = Moeda(ind.SaldoPrevisto);
        EntradasMes = Moeda(ind.EntradasMes);
        SaidasMes = Moeda(ind.SaidasMes);
        AReceber = Moeda(ind.AReceber);
        APagar = Moeda(ind.APagar);
        VencidosTexto = ind.VencidosQuantidade switch
        {
            0 => null,
            1 => $"1 conta vencida, somando {Moeda(ind.VencidosValor)}",
            var n => $"{n} contas vencidas, somando {Moeda(ind.VencidosValor)}",
        };

        Historico = dados.Historico
            .Select(h => new ChartBarGroup(ChartFormat.MesCurto(h.Mes), (double)h.Entradas, (double)h.Saidas))
            .ToList();
        HistoricoSelecionado = Historico.Count - 1;
        OnHistoricoSelecionadoChanged(HistoricoSelecionado);

        AplicarPrevisao();

        Categorias = dados.DespesasPorCategoria
            .Select(c => new ChartCategory(c.Categoria, (double)c.Total))
            .ToList();

        Receitas = dados.ReceitasPorCategoria
            .Select(c => new ChartCategory(c.Categoria, (double)c.Total))
            .ToList();
        var totalReceitas = dados.ReceitasPorCategoria.Sum(c => c.Total);
        var maior = dados.ReceitasPorCategoria.FirstOrDefault(c => c.Categoria != "Outras");
        MaiorReceita = maior is null || totalReceitas == 0
            ? string.Empty
            : $"{maior.Categoria}: {Moeda(maior.Total)} ({(maior.Total / totalReceitas).ToString("P0", ChartFormat.PtBr)} das receitas)";

        ProximosVencimentos = dados.ProximosVencimentos.Select(v => new VencimentoItem(
            v.Descricao,
            DataVencimentoTexto(v),
            (v.Tipo == "entrada" ? "+ " : "- ") + Moeda(v.Valor),
            v.Tipo == "entrada" ? IconFont.Entrada : IconFont.Saida,
            v.Tipo == "entrada",
            v.Vencido)).ToList();

        HasData = true;
    }

    partial void OnHistoricoSelecionadoChanged(int value)
    {
        if (_dados is null || value < 0 || value >= _dados.Historico.Count) return;

        var mes = _dados.Historico[value];
        var resultado = mes.Entradas - mes.Saidas;
        HistoricoMes = Capitalizar(ChartFormat.MesLongo(mes.Mes));
        HistoricoEntradas = Moeda(mes.Entradas);
        HistoricoSaidas = Moeda(mes.Saidas);
        HistoricoResultado = $"Resultado do mês: {(resultado >= 0 ? "+" : string.Empty)}{Moeda(resultado)}";
    }

    private List<PrevisaoMensal> PrevisaoAtual =>
        (IsPrevisaoHistorica ? _dados?.PrevisaoHistorica : _dados?.Previsao) ?? [];

    // Preenche gráfico, leitura e aviso com a série do modo escolhido, mantendo o mês selecionado.
    private void AplicarPrevisao()
    {
        var meses = PrevisaoAtual;
        var selecionado = Math.Clamp(PrevisaoSelecionada, 0, Math.Max(0, meses.Count - 1));

        Previsao = meses.Select(p => new ChartPoint(ChartFormat.MesCurto(p.Mes), (double)p.Saldo)).ToList();
        PrevisaoSelecionada = meses.Count > 0 ? selecionado : -1;
        OnPrevisaoSelecionadaChanged(PrevisaoSelecionada);
        OnPropertyChanged(nameof(PrevisaoSubtitulo));

        var primeiroNegativo = meses.FirstOrDefault(p => p.Saldo < 0);
        AvisoSaldoNegativo = primeiroNegativo is null
            ? null
            : $"O saldo previsto fica negativo em {ChartFormat.MesLongo(primeiroNegativo.Mes)} ({Moeda(primeiroNegativo.Saldo)}).";
    }

    partial void OnPrevisaoSelecionadaChanged(int value)
    {
        var meses = PrevisaoAtual;
        if (value < 0 || value >= meses.Count) return;

        var mes = meses[value];
        var (entradas, saidas) = IsPrevisaoHistorica ? ("Entradas", "saídas") : ("A receber", "a pagar");
        PrevisaoMes = $"Saldo previsto ao fim de {ChartFormat.MesLongo(mes.Mes)}";
        PrevisaoSaldo = Moeda(mes.Saldo);
        PrevisaoDetalhe = $"{entradas} {Moeda(mes.Entradas)} · {saidas} {Moeda(mes.Saidas)}"
            + (value == 0 && IsPrevisaoLancamentos ? " (inclui vencidos)" : string.Empty);
    }

    private static string DataVencimentoTexto(VencimentoResumo v)
    {
        if (!DateTime.TryParse(v.DataVencimento, CultureInfo.InvariantCulture, DateTimeStyles.None, out var data))
            return v.DataVencimento;

        var texto = data.ToString("dd/MM/yyyy", ChartFormat.PtBr);
        return v.Vencido ? $"Venceu em {texto}" : $"Vence em {texto}";
    }

    private static string Moeda(decimal valor) => valor.ToString("C2", ChartFormat.PtBr);

    private static string Capitalizar(string texto) =>
        texto.Length == 0 ? texto : char.ToUpper(texto[0], ChartFormat.PtBr) + texto[1..];
}
