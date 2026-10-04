namespace checkfrota_front.Charts;

// Linha de série única com área suave até a base (ex.: saldo previsto). Por padrão o eixo inclui o
// zero, para que um saldo negativo apareça abaixo da linha de base. Pontos sem valor (double.NaN)
// interrompem a linha, em vez de despencar para zero.
public class LineChart : MonthAxisChart
{
    public static readonly BindableProperty ItemsProperty = BindableProperty.Create(
        nameof(Items), typeof(IReadOnlyList<ChartPoint>), typeof(LineChart), null, propertyChanged: Redesenhar);

    public IReadOnlyList<ChartPoint>? Items
    {
        get => (IReadOnlyList<ChartPoint>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public static readonly BindableProperty IncluirZeroProperty = BindableProperty.Create(
        nameof(IncluirZero), typeof(bool), typeof(LineChart), true, propertyChanged: Redesenhar);

    // False aproxima o eixo dos valores, para variações pequenas ficarem visíveis (ex.: km/l).
    public bool IncluirZero
    {
        get => (bool)GetValue(IncluirZeroProperty);
        set => SetValue(IncluirZeroProperty, value);
    }

    protected override int Quantidade => Items?.Count ?? 0;

    protected override void DesenharGrafico(ICanvas canvas, RectF area)
    {
        var itens = Items;
        if (itens is null || itens.Count == 0) return;

        var plot = AreaDoPlot(area);
        var rotulos = itens.Select(i => i.Rotulo).ToList();
        var valores = itens.Where(i => !double.IsNaN(i.Valor)).Select(i => i.Valor).ToList();

        DesenharFaixaSelecionada(canvas, plot, SelectedIndex);

        if (valores.Count == 0)
        {
            DesenharRotulosX(canvas, plot, rotulos, SelectedIndex);
            return;
        }

        var (menor, maior) = (valores.Min(), valores.Max());
        if (!IncluirZero)
        {
            // Folga de 10% (ou de 1, com um único valor) para a linha não encostar nas bordas.
            var folga = maior > menor ? (maior - menor) * 0.1 : Math.Max(1, Math.Abs(maior) * 0.1);
            (menor, maior) = (menor - folga, maior + folga);
        }

        var (minimo, maximo, passo) = IncluirZero
            ? ChartFormat.EscalaRedonda(Math.Min(0, menor), Math.Max(0, maior))
            : ChartFormat.EscalaRedonda(menor, maior);

        DesenharEixoY(canvas, plot, minimo, maximo, passo);

        var cor = Cor("ChartSaldo", Colors.Blue);
        var superficie = Cor("CardBg", Colors.White);
        var yBase = PosicaoY(plot, IncluirZero ? 0 : minimo, minimo, maximo);
        var pontos = itens
            .Select((item, i) => double.IsNaN(item.Valor) ? (PointF?)null : new PointF(CentroDaFaixa(plot, i), PosicaoY(plot, item.Valor, minimo, maximo)))
            .ToList();

        foreach (var trecho in Trechos(pontos))
        {
            var areaPreenchida = new PathF();
            areaPreenchida.MoveTo(trecho[0].X, yBase);
            foreach (var ponto in trecho) areaPreenchida.LineTo(ponto);
            areaPreenchida.LineTo(trecho[^1].X, yBase);
            areaPreenchida.Close();
            canvas.FillColor = cor.WithAlpha(0.10f);
            canvas.FillPath(areaPreenchida);

            if (trecho.Count < 2) continue;

            var linha = new PathF();
            linha.MoveTo(trecho[0]);
            foreach (var ponto in trecho.Skip(1)) linha.LineTo(ponto);
            canvas.StrokeColor = cor;
            canvas.StrokeSize = 2;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.StrokeLineJoin = LineJoin.Round;
            canvas.DrawPath(linha);
        }

        for (var i = 0; i < pontos.Count; i++)
        {
            if (pontos[i] is not PointF ponto) continue;

            var raio = i == SelectedIndex ? 6f : 4f;
            canvas.FillColor = superficie;
            canvas.FillCircle(ponto, raio + 2);
            canvas.FillColor = cor;
            canvas.FillCircle(ponto, raio);
        }

        DesenharRotulosX(canvas, plot, rotulos, SelectedIndex);
    }

    // Sequências de pontos consecutivos com valor.
    private static IEnumerable<List<PointF>> Trechos(IReadOnlyList<PointF?> pontos)
    {
        var atual = new List<PointF>();
        foreach (var ponto in pontos)
        {
            if (ponto is PointF valido)
            {
                atual.Add(valido);
                continue;
            }

            if (atual.Count > 0) yield return atual;
            atual = new List<PointF>();
        }

        if (atual.Count > 0) yield return atual;
    }
}
