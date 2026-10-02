namespace checkfrota_front.Charts;

// Ranking em barras horizontais: nome e valor (com % do total) em texto, barra proporcional ao maior item.
// Todos os valores ficam escritos, então não há seleção por toque.
public class RankingBarChart : ChartViewBase
{
    private const float AlturaDaLinha = 46;
    private const float AlturaDaBarra = 8;

    public static readonly BindableProperty ItemsProperty = BindableProperty.Create(
        nameof(Items), typeof(IReadOnlyList<ChartCategory>), typeof(RankingBarChart), null, propertyChanged: OnItemsChanged);

    public IReadOnlyList<ChartCategory>? Items
    {
        get => (IReadOnlyList<ChartCategory>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public static readonly BindableProperty BarColorKeyProperty = BindableProperty.Create(
        nameof(BarColorKey), typeof(string), typeof(RankingBarChart), "ChartSaida", propertyChanged: Redesenhar);

    // Chave da cor no tema (ex.: "ChartSaida" para despesas).
    public string BarColorKey
    {
        get => (string)GetValue(BarColorKeyProperty);
        set => SetValue(BarColorKeyProperty, value);
    }

    private static void OnItemsChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var grafico = (RankingBarChart)bindable;
        grafico.HeightRequest = (grafico.Items?.Count ?? 0) * AlturaDaLinha;
        grafico.Invalidate();
    }

    protected override void DesenharGrafico(ICanvas canvas, RectF area)
    {
        var itens = Items;
        if (itens is null || itens.Count == 0) return;

        var maior = itens.Max(i => i.Valor);
        var total = itens.Sum(i => i.Valor);
        var corTexto = Cor("TextPrimary", Colors.Black);
        var corSecundaria = Cor("TextSecondary", Colors.Gray);
        var corBarra = Cor(BarColorKey, Colors.Orange);
        var trilho = corSecundaria.WithAlpha(0.15f);

        for (var i = 0; i < itens.Count; i++)
        {
            var topo = area.Top + i * AlturaDaLinha;
            var percentual = total > 0 ? itens[i].Valor / total : 0;
            var valor = $"{ChartFormat.Moeda(itens[i].Valor)} · {percentual.ToString("P0", ChartFormat.PtBr)}";

            DesenharTexto(canvas, valor, area.Left, topo, area.Width, 20, corSecundaria, 12, HorizontalAlignment.Right);
            DesenharTexto(canvas, Abreviar(itens[i].Rotulo, 22), area.Left, topo, area.Width * 0.55f, 20, corTexto, 13);

            var yBarra = topo + 24;
            canvas.FillColor = trilho;
            canvas.FillRoundedRectangle(area.Left, yBarra, area.Width, AlturaDaBarra, AlturaDaBarra / 2);

            var largura = maior > 0 ? (float)(itens[i].Valor / maior) * area.Width : 0;
            if (largura <= 0) continue;

            canvas.FillColor = corBarra;
            canvas.FillRoundedRectangle(area.Left, yBarra, Math.Max(largura, AlturaDaBarra), AlturaDaBarra, AlturaDaBarra / 2);
        }
    }

    // O canvas não reticencia texto sozinho; corta nomes longos para não invadir o valor à direita.
    private static string Abreviar(string texto, int maximo) =>
        texto.Length <= maximo ? texto : texto[..(maximo - 1)].TrimEnd() + "…";
}
