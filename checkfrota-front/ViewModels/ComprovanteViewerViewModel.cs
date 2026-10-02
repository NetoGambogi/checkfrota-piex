using checkfrota_front.Models;
using checkfrota_front.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Net;
using System.Text;

namespace checkfrota_front.ViewModels;

// Exibe o comprovante dentro do app: as páginas (imagens) são empilhadas numa WebView,
// que oferece zoom por pinça sem depender de visualizador externo.
public partial class ComprovanteViewerViewModel : AdminSectionViewModelBase, IQueryAttributable
{
    [ObservableProperty]
    private string nome = "Comprovante";

    [ObservableProperty]
    private HtmlWebViewSource? conteudo;

    [ObservableProperty]
    private bool isLoading = true;

    private IReadOnlyList<string> _paginas = [];

    public ComprovanteViewerViewModel(ApiAuthService apiAuth) : base(apiAuth)
    {
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("comprovante", out var obj) && obj is ComprovanteResumo comprovante)
        {
            Nome = comprovante.Nome;
            _paginas = comprovante.Paginas.Count > 0 ? comprovante.Paginas : [comprovante.Url];
            Conteudo = new HtmlWebViewSource { Html = MontarHtml(_paginas) };
        }
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        await LoadAvatarAsync();
    }

    [RelayCommand]
    private void ConteudoCarregado()
    {
        IsLoading = false;
    }

    private static string MontarHtml(IReadOnlyList<string> paginas)
    {
        var fundo = CorHex("PageBg", "#FFFFFF");
        var texto = CorHex("TextSecondary", "#6B7280");

        var html = new StringBuilder();
        html.Append("<!doctype html><html><head><meta charset=\"utf-8\">");
        html.Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1, minimum-scale=1, maximum-scale=6, user-scalable=yes\">");
        html.Append("<style>");
        html.Append($"html,body{{margin:0;background:{fundo};}}");
        html.Append("body{padding:12px;}");
        html.Append("img{display:block;width:100%;height:auto;margin:0 auto 12px;border-radius:6px;background:#FFFFFF;}");
        html.Append($".erro{{color:{texto};font:14px sans-serif;text-align:center;padding:32px 12px;}}");
        html.Append("</style></head><body>");

        for (var i = 0; i < paginas.Count; i++)
        {
            var erro = WebUtility.HtmlEncode($"Não foi possível carregar a página {i + 1}.");
            html.Append($"<img src=\"{WebUtility.HtmlEncode(paginas[i])}\" alt=\"Página {i + 1}\" ");
            html.Append($"onerror=\"this.outerHTML='<div class=&quot;erro&quot;>{erro}</div>'\">");
        }

        html.Append("</body></html>");
        return html.ToString();
    }

    private static string CorHex(string chave, string padrao) =>
        Application.Current?.Resources.TryGetValue(chave, out var valor) == true && valor is Color cor
            ? cor.ToRgbaHex()[..7]
            : padrao;
}
