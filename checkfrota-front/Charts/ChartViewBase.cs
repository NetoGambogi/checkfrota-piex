namespace checkfrota_front.Charts;

// Base dos gráficos desenhados com GraphicsView (sem biblioteca externa). Cuida do toque/arraste
// para selecionar um ponto (SelectedIndex, two-way) e redesenha quando o tema do app muda.
public abstract class ChartViewBase : GraphicsView, IDrawable
{
    public static readonly BindableProperty SelectedIndexProperty = BindableProperty.Create(
        nameof(SelectedIndex), typeof(int), typeof(ChartViewBase), -1, BindingMode.TwoWay, propertyChanged: Redesenhar);

    public int SelectedIndex
    {
        get => (int)GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    private RectF _area;

    protected ChartViewBase()
    {
        Drawable = this;
        BackgroundColor = Colors.Transparent;
        StartInteraction += (_, e) => Selecionar(e.Touches);
        DragInteraction += (_, e) => Selecionar(e.Touches);
        Loaded += (_, _) => { if (Application.Current is { } app) app.RequestedThemeChanged += OnTemaAlterado; };
        Unloaded += (_, _) => { if (Application.Current is { } app) app.RequestedThemeChanged -= OnTemaAlterado; };
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        _area = dirtyRect;
        canvas.Antialias = true;
        canvas.Font = Microsoft.Maui.Graphics.Font.Default;
        DesenharGrafico(canvas, dirtyRect);
    }

    protected abstract void DesenharGrafico(ICanvas canvas, RectF area);

    // Índice do item sob o ponto tocado, ou -1 quando o gráfico não tem seleção.
    protected virtual int IndiceEm(PointF ponto, RectF area) => -1;

    protected static void Redesenhar(BindableObject bindable, object oldValue, object newValue) =>
        ((ChartViewBase)bindable).Invalidate();

    protected static Color Cor(string chave, Color padrao) =>
        Application.Current?.Resources.TryGetValue(chave, out var valor) == true && valor is Color cor ? cor : padrao;

    protected static void DesenharTexto(ICanvas canvas, string texto, float x, float y, float largura, float altura,
        Color cor, float tamanho, HorizontalAlignment horizontal = HorizontalAlignment.Left,
        VerticalAlignment vertical = VerticalAlignment.Center, bool negrito = false)
    {
        canvas.FontColor = cor;
        canvas.FontSize = tamanho;
        canvas.Font = negrito ? Microsoft.Maui.Graphics.Font.DefaultBold : Microsoft.Maui.Graphics.Font.Default;
        canvas.DrawString(texto, x, y, largura, altura, horizontal, vertical);
    }

    private void Selecionar(PointF[] toques)
    {
        if (toques.Length == 0) return;

        var indice = IndiceEm(toques[0], _area);
        if (indice >= 0 && indice != SelectedIndex)
            SelectedIndex = indice;
    }

    private void OnTemaAlterado(object? sender, AppThemeChangedEventArgs e) => Dispatcher.Dispatch(Invalidate);
}
