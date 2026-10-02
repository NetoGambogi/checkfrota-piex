namespace checkfrota_front.Charts;

// Base dos gráficos com um item por mês no eixo X (uma "faixa" por mês) e um único eixo Y em R$.
public abstract class MonthAxisChart : ChartViewBase
{
    protected const float MargemEsquerda = 58;
    protected const float MargemDireita = 6;
    protected const float MargemTopo = 10;
    protected const float MargemBase = 24;

    protected abstract int Quantidade { get; }

    protected static RectF AreaDoPlot(RectF area) => new(
        area.Left + MargemEsquerda,
        area.Top + MargemTopo,
        area.Width - MargemEsquerda - MargemDireita,
        area.Height - MargemTopo - MargemBase);

    protected float LarguraDaFaixa(RectF plot) => Quantidade == 0 ? 0 : plot.Width / Quantidade;

    protected float CentroDaFaixa(RectF plot, int indice) => plot.Left + LarguraDaFaixa(plot) * (indice + 0.5f);

    protected static float PosicaoY(RectF plot, double valor, double minimo, double maximo) =>
        (float)(plot.Bottom - (valor - minimo) / (maximo - minimo) * plot.Height);

    protected override int IndiceEm(PointF ponto, RectF area)
    {
        var plot = AreaDoPlot(area);
        if (Quantidade == 0 || ponto.X < plot.Left - 8 || ponto.X > plot.Right + 8) return -1;

        return Math.Clamp((int)((ponto.X - plot.Left) / LarguraDaFaixa(plot)), 0, Quantidade - 1);
    }

    // Grade horizontal discreta, linha do zero mais forte e rótulos do eixo Y à esquerda.
    protected static void DesenharEixoY(ICanvas canvas, RectF plot, double minimo, double maximo, double passo)
    {
        var grade = Cor("ChartGrid", Colors.LightGray);
        var linhaBase = Cor("ChartBaseline", Colors.Gray);
        var texto = Cor("TextSecondary", Colors.Gray);

        canvas.StrokeSize = 1;
        for (var valor = minimo; valor <= maximo + passo / 2; valor += passo)
        {
            var y = PosicaoY(plot, valor, minimo, maximo);
            canvas.StrokeColor = Math.Abs(valor) < passo / 1000 ? linhaBase : grade;
            canvas.DrawLine(plot.Left, y, plot.Right, y);
            DesenharTexto(canvas, ChartFormat.MoedaCompacta(valor), 0, y - 8, MargemEsquerda - 8, 16,
                texto, 10, HorizontalAlignment.Right);
        }
    }

    // Rótulos dos meses; com muitos meses, mostra um sim, um não (sempre incluindo o último e o selecionado).
    protected void DesenharRotulosX(ICanvas canvas, RectF plot, IReadOnlyList<string> rotulos, int selecionado)
    {
        var texto = Cor("TextSecondary", Colors.Gray);
        var destaque = Cor("TextPrimary", Colors.Black);
        var faixa = LarguraDaFaixa(plot);
        var pular = rotulos.Count > 8;

        for (var i = 0; i < rotulos.Count; i++)
        {
            var visivel = !pular || (rotulos.Count - 1 - i) % 2 == 0 || i == selecionado;
            if (!visivel) continue;

            DesenharTexto(canvas, rotulos[i], plot.Left + faixa * i - 6, plot.Bottom + 4, faixa + 12, 18,
                i == selecionado ? destaque : texto, 10, HorizontalAlignment.Center, negrito: i == selecionado);
        }
    }

    protected void DesenharFaixaSelecionada(ICanvas canvas, RectF plot, int selecionado)
    {
        if (selecionado < 0 || selecionado >= Quantidade) return;

        var faixa = LarguraDaFaixa(plot);
        canvas.FillColor = Cor("TextPrimary", Colors.Black).WithAlpha(0.06f);
        canvas.FillRoundedRectangle(plot.Left + faixa * selecionado + 2, plot.Top - 6, faixa - 4, plot.Height + 6, 6);
    }
}
