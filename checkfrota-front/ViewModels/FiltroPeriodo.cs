using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace checkfrota_front.ViewModels;

/// <summary>
/// Período das listagens da frota (atalhos "Este mês", "3 meses", "12 meses" ou datas livres), para
/// buscar na API só o intervalo escolhido em vez do histórico inteiro. Exibido pela view
/// <c>Views/Shared/FiltroPeriodoView</c>; a tela dona recarrega a lista no evento <see cref="Alterado"/>.
/// </summary>
public partial class FiltroPeriodo : ObservableObject
{
    public const string EsteMes = "mes";
    public const string TresMeses = "3meses";
    public const string DozeMeses = "12meses";
    public const string Personalizado = "personalizado";

    // Enquanto um atalho ajusta as duas datas, as mudanças não disparam recargas; o evento sai uma vez só.
    private bool _ajustando;

    public event EventHandler? Alterado;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Resumo))]
    private DateTime dataInicio;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Resumo))]
    private DateTime dataFim;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEsteMes), nameof(IsTresMeses), nameof(IsDozeMeses), nameof(Resumo))]
    private string atalho = TresMeses;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChevronRotation))]
    private bool isExpandido;

    public bool IsEsteMes => Atalho == EsteMes;
    public bool IsTresMeses => Atalho == TresMeses;
    public bool IsDozeMeses => Atalho == DozeMeses;
    public double ChevronRotation => IsExpandido ? 90 : 0;

    public string Resumo
    {
        get
        {
            var nome = Atalho switch
            {
                EsteMes => "Este mês",
                TresMeses => "Últimos 3 meses",
                DozeMeses => "Últimos 12 meses",
                _ => "Período",
            };
            return $"{nome}: {DataInicio:dd/MM/yyyy} a {DataFim:dd/MM/yyyy}";
        }
    }

    public FiltroPeriodo(string atalhoInicial = TresMeses)
    {
        AplicarAtalho(atalhoInicial);
    }

    [RelayCommand]
    private void SelecionarAtalho(string novo)
    {
        if (novo == Atalho) return;

        AplicarAtalho(novo);
        Alterado?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void AlternarExpandido() => IsExpandido = !IsExpandido;

    // Os atalhos vão do 1º dia do mês inicial até o fim do mês atual (inclui registros agendados no mês).
    private void AplicarAtalho(string novo)
    {
        var inicioDoMes = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var mesesAtras = novo switch
        {
            EsteMes => 0,
            DozeMeses => 11,
            _ => 2,
        };

        _ajustando = true;
        Atalho = novo;
        DataInicio = inicioDoMes.AddMonths(-mesesAtras);
        DataFim = inicioDoMes.AddMonths(1).AddDays(-1);
        _ajustando = false;
    }

    partial void OnDataInicioChanged(DateTime value)
    {
        if (_ajustando) return;

        // Mantém o período válido: início depois do fim arrasta o fim junto.
        _ajustando = true;
        if (value > DataFim) DataFim = value;
        Atalho = Personalizado;
        _ajustando = false;

        Alterado?.Invoke(this, EventArgs.Empty);
    }

    partial void OnDataFimChanged(DateTime value)
    {
        if (_ajustando) return;

        _ajustando = true;
        if (value < DataInicio) DataInicio = value;
        Atalho = Personalizado;
        _ajustando = false;

        Alterado?.Invoke(this, EventArgs.Empty);
    }
}
