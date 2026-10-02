namespace checkfrota_front.Charts;

// Colunas agrupadas por mês: entradas (série 1) e saídas (série 2), crescendo da linha do zero.
public class MonthlyBarChart : MonthAxisChart
{
    private const float LarguraMaximaDaBarra = 16;
    private const float EspacoEntreBarras = 2;
    private const float Arredondamento = 4;

    public static readonly BindableProperty ItemsProperty = BindableProperty.Create(
        nameof(Items), typeof(IReadOnlyList<ChartBarGroup>), typeof(MonthlyBarChart), null, propertyChanged: Redesenhar);

    public IReadOnlyList<ChartBarGroup>? Items
    {
        get => (IReadOnlyList<ChartBarGroup>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    protected override int Quantidade => Items?.Count ?? 0;

    protected override void DesenharGrafico(ICanvas canvas, RectF area)
    {
        var itens = Items;
        if (itens is null || itens.Count == 0) return;

        var plot = AreaDoPlot(area);
        var maior = itens.Max(i => Math.Max(i.Entradas, i.Saidas));
        var (minimo, maximo, passo) = ChartFormat.EscalaRedonda(0, maior);

        DesenharFaixaSelecionada(canvas, plot, SelectedIndex);
        DesenharEixoY(canvas, plot, minimo, maximo, passo);

        var faixa = LarguraDaFaixa(plot);
        var largura = Math.Min(LarguraMaximaDaBarra, (faixa * 0.7f - EspacoEntreBarras) / 2);
        var corEntrada = Cor("ChartEntrada", Colors.Blue);
        var corSaida = Cor("ChartSaida", Colors.Orange);

        for (var i = 0; i < itens.Count; i++)
        {
            var inicio = CentroDaFaixa(plot, i) - largura - EspacoEntreBarras / 2;
            DesenharBarra(canvas, plot, inicio, largura, itens[i].Entradas, minimo, maximo, corEntrada);
            DesenharBarra(canvas, plot, inicio + largura + EspacoEntreBarras, largura, itens[i].Saidas, minimo, maximo, corSaida);
        }

        DesenharRotulosX(canvas, plot, itens.Select(i => i.Rotulo).ToList(), SelectedIndex);
    }

    private static void DesenharBarra(ICanvas canvas, RectF plot, float x, float largura, double valor,
        double minimo, double maximo, Color cor)
    {
        if (valor <= 0) return;

        var topo = PosicaoY(plot, valor, minimo, maximo);
        var altura = Math.Max(2, plot.Bottom - topo);
        var raio = Math.Min(Arredondamento, Math.Min(altura, largura) / 2);

        canvas.FillColor = cor;
        canvas.FillRoundedRectangle(x, plot.Bottom - altura, largura, altura, raio, raio, 0, 0);
    }
}
