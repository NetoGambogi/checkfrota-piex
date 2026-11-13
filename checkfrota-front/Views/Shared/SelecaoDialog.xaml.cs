using checkfrota_front.ViewModels;

namespace checkfrota_front.Views.Shared;

/// <summary>
/// Diálogo com lista pesquisável para escolher um item (ex.: caminhão ou rota), desenhado como uma
/// camada sobre a página atual: uma página modal no Android tem fundo opaco e esconderia a tela de
/// trás. Abra com <see cref="EscolherAsync"/>: devolve a opção tocada, ou null se for fechado.
/// </summary>
public partial class SelecaoDialog : ContentView
{
    private const double OpacidadeFundo = 0.6;

    private readonly TaskCompletionSource<OpcaoSelecao?> _resultado = new();
    private readonly IReadOnlyList<OpcaoSelecao> _opcoes;
    private bool _fechando;

    private SelecaoDialog(string titulo, string placeholder, IReadOnlyList<OpcaoSelecao> opcoes)
    {
        InitializeComponent();

        _opcoes = opcoes;
        TituloLabel.Text = titulo;
        Busca.Placeholder = placeholder;
        Lista.ItemsSource = opcoes;

        // A lista ocupa no máximo ~55% da tela, para o diálogo não encostar nas bordas.
        var tela = DeviceDisplay.Current.MainDisplayInfo;
        Lista.MaximumHeightRequest = Math.Max(240, tela.Height / tela.Density * 0.55);
    }

    public static async Task<OpcaoSelecao?> EscolherAsync(string titulo, string placeholder, IReadOnlyList<OpcaoSelecao> opcoes)
    {
        var pagina = Shell.Current?.CurrentPage as ContentPage
            ?? Application.Current?.Windows.FirstOrDefault()?.Page as ContentPage;
        if (pagina?.Content is null) return null;

        var dialogo = new SelecaoDialog(titulo, placeholder, opcoes);
        var desfazer = Sobrepor(pagina, dialogo);

        try
        {
            await dialogo.AnimarEntradaAsync();
            return await dialogo._resultado.Task;
        }
        finally
        {
            desfazer();
        }
    }

    /// <summary>
    /// Põe o diálogo por cima de todo o conteúdo da página. Se a raiz já é um Grid, entra nele (sem
    /// mexer no conteúdo, para não perder a rolagem); senão, a raiz vai para dentro de um Grid novo.
    /// Devolve a ação que tira o diálogo e deixa a página como estava.
    /// </summary>
    private static Action Sobrepor(ContentPage pagina, SelecaoDialog dialogo)
    {
        dialogo.ZIndex = 1000;

        if (pagina.Content is Grid raiz)
        {
            Grid.SetRowSpan(dialogo, Math.Max(1, raiz.RowDefinitions.Count));
            Grid.SetColumnSpan(dialogo, Math.Max(1, raiz.ColumnDefinitions.Count));
            raiz.Add(dialogo);
            return () => raiz.Remove(dialogo);
        }

        var original = pagina.Content;
        pagina.Content = new Grid { Children = { original, dialogo } };
        return () =>
        {
            ((Grid)pagina.Content).Clear();
            pagina.Content = original;
        };
    }

    /// <summary>
    /// Só aparece depois de montado, já com tema e opacidade aplicados, e entra com um fade curto.
    /// </summary>
    private async Task AnimarEntradaAsync()
    {
        await Task.Delay(50);
        IsVisible = true;
        await Task.WhenAll(
            Fundo.FadeToAsync(OpacidadeFundo, 150),
            Cartao.FadeToAsync(1, 150));
    }

    private void OnBuscaAlterada(object? sender, TextChangedEventArgs e)
    {
        var termo = e.NewTextValue ?? string.Empty;
        Lista.ItemsSource = _opcoes.Where(o => o.Corresponde(termo)).ToList();
    }

    private async void OnOpcaoTocada(object? sender, TappedEventArgs e) =>
        await FecharAsync(e.Parameter as OpcaoSelecao);

    private async void OnFecharTocado(object? sender, TappedEventArgs e) => await FecharAsync(null);

    private async Task FecharAsync(OpcaoSelecao? escolha)
    {
        if (_fechando) return;
        _fechando = true;

        Busca.Unfocus();
        await Task.WhenAll(Fundo.FadeToAsync(0, 120), Cartao.FadeToAsync(0, 120));
        _resultado.TrySetResult(escolha);
    }
}
