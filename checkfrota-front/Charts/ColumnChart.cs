namespace checkfrota_front.Charts;

// Colunas de série única por mês (ex.: lucro da rota mais lucrativa). Valores negativos descem
// abaixo da linha do zero; meses sem valor (double.NaN) ficam vazios.
public class ColumnChart : MonthAxisChart
{
    private const float LarguraMaximaDaBarra = 22;
    private const float Arredondamento = 4;

    public static readonly BindableProperty ItemsProperty = BindableProperty.Create(
        nameof(Items), typeof(IReadOnlyList<ChartPoint>), typeof(ColumnChart), null, propertyChanged: Redesenhar);

    public IReadOnlyList<ChartPoint>? Items
    {
        get => (IReadOnlyList<ChartPoint>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public static readonly BindableProperty BarColorKeyProperty = BindableProperty.Create(
        nameof(BarColorKey), typeof(string), typeof(ColumnChart), "ChartEntrada", propertyChanged: Redesenhar);

    // Chave da cor no tema (ex.: "ChartEntrada").
    public string BarColorKey
    {
        get => (string)GetValue(BarColorKeyProperty);
        set => SetValue(BarColorKeyProperty, value);
    }

    protected override int Quantidade => Items?.Count ?? 0;

    protected override void DesenharGrafico(ICanvas canvas, RectF area)
    {
        var itens = Items;
        if (itens is null || itens.Count == 0) return;

        var plot = AreaDoPlot(area);
        var valores = itens.Select(i => i.Valor).Where(v => !double.IsNaN(v)).DefaultIfEmpty(0).ToList();
        var (minimo, maximo, passo) = ChartFormat.EscalaRedonda(Math.Min(0, valores.Min()), Math.Max(0, valores.Max()));

        DesenharFaixaSelecionada(canvas, plot, SelectedIndex);
        DesenharEixoY(canvas, plot, minimo, maximo, passo);

        var largura = Math.Min(LarguraMaximaDaBarra, LarguraDaFaixa(plot) * 0.55f);
        var cor = Cor(BarColorKey, Colors.Blue);
        var corNegativa = Cor("ChartSaida", Colors.Orange);
        var yZero = PosicaoY(plot, 0, minimo, maximo);

        for (var i = 0; i < itens.Count; i++)
        {
            var valor = itens[i].Valor;
            if (double.IsNaN(valor) || valor == 0) continue;

            var y = PosicaoY(plot, valor, minimo, maximo);
            var altura = Math.Max(2, Math.Abs(yZero - y));
            var x = CentroDaFaixa(plot, i) - largura / 2;
            var raio = Math.Min(Arredondamento, Math.Min(altura, largura) / 2);

            canvas.FillColor = valor > 0 ? cor : corNegativa;
            if (valor > 0)
                canvas.FillRoundedRectangle(x, yZero - altura, largura, altura, raio, raio, 0, 0);
            else
                canvas.FillRoundedRectangle(x, yZero, largura, altura, 0, 0, raio, raio);
        }

        DesenharRotulosX(canvas, plot, itens.Select(i => i.Rotulo).ToList(), SelectedIndex);
    }
}
