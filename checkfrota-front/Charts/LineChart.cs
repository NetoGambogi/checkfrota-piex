namespace checkfrota_front.Charts;

// Linha de série única com área suave até o zero (ex.: saldo previsto). O eixo sempre inclui o zero,
// para que um saldo negativo apareça abaixo da linha de base.
public class LineChart : MonthAxisChart
{
    public static readonly BindableProperty ItemsProperty = BindableProperty.Create(
        nameof(Items), typeof(IReadOnlyList<ChartPoint>), typeof(LineChart), null, propertyChanged: Redesenhar);

    public IReadOnlyList<ChartPoint>? Items
    {
        get => (IReadOnlyList<ChartPoint>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    protected override int Quantidade => Items?.Count ?? 0;

    protected override void DesenharGrafico(ICanvas canvas, RectF area)
    {
        var itens = Items;
        if (itens is null || itens.Count == 0) return;

        var plot = AreaDoPlot(area);
        var (minimo, maximo, passo) = ChartFormat.EscalaRedonda(
            Math.Min(0, itens.Min(i => i.Valor)), Math.Max(0, itens.Max(i => i.Valor)));

        DesenharFaixaSelecionada(canvas, plot, SelectedIndex);
        DesenharEixoY(canvas, plot, minimo, maximo, passo);

        var cor = Cor("ChartSaldo", Colors.Blue);
        var superficie = Cor("CardBg", Colors.White);
        var pontos = itens.Select((item, i) => new PointF(CentroDaFaixa(plot, i), PosicaoY(plot, item.Valor, minimo, maximo))).ToList();
        var yZero = PosicaoY(plot, 0, minimo, maximo);

        var areaPreenchida = new PathF();
        areaPreenchida.MoveTo(pontos[0].X, yZero);
        foreach (var ponto in pontos) areaPreenchida.LineTo(ponto);
        areaPreenchida.LineTo(pontos[^1].X, yZero);
        areaPreenchida.Close();
        canvas.FillColor = cor.WithAlpha(0.10f);
        canvas.FillPath(areaPreenchida);

        if (pontos.Count > 1)
        {
            var linha = new PathF();
            linha.MoveTo(pontos[0]);
            foreach (var ponto in pontos.Skip(1)) linha.LineTo(ponto);
            canvas.StrokeColor = cor;
            canvas.StrokeSize = 2;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.StrokeLineJoin = LineJoin.Round;
            canvas.DrawPath(linha);
        }

        for (var i = 0; i < pontos.Count; i++)
        {
            var raio = i == SelectedIndex ? 6f : 4f;
            canvas.FillColor = superficie;
            canvas.FillCircle(pontos[i], raio + 2);
            canvas.FillColor = cor;
            canvas.FillCircle(pontos[i], raio);
        }

        DesenharRotulosX(canvas, plot, itens.Select(i => i.Rotulo).ToList(), SelectedIndex);
    }
}
