using checkfrota_front.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Globalization;

namespace checkfrota_front.ViewModels;

public partial class NotificacaoRowViewModel : ObservableObject
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public string Id { get; }
    public string Titulo { get; }
    public string Mensagem { get; }
    public string? Destino { get; }
    public string QuandoTexto { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NaoLida))]
    private bool lida;

    public bool NaoLida => !Lida;

    public NotificacaoRowViewModel(NotificacaoItem item)
    {
        Id = item.Id;
        Titulo = item.Titulo;
        Mensagem = item.Mensagem;
        Destino = item.Destino;
        Lida = item.Lida;
        QuandoTexto = FormatarQuando(item.CriadaEm);
    }

    private static string FormatarQuando(string? criadaEm)
    {
        if (!DateTimeOffset.TryParse(criadaEm, CultureInfo.InvariantCulture, DateTimeStyles.None, out var data))
            return string.Empty;

        var local = data.ToLocalTime();
        var decorrido = DateTimeOffset.Now - local;

        if (decorrido.TotalMinutes < 1) return "agora";
        if (decorrido.TotalMinutes < 60) return $"há {(int)decorrido.TotalMinutes} min";
        if (local.Date == DateTime.Today) return $"hoje, {local:HH:mm}";
        if (local.Date == DateTime.Today.AddDays(-1)) return $"ontem, {local:HH:mm}";

        return local.ToString("dd/MM/yyyy HH:mm", PtBr);
    }
}
