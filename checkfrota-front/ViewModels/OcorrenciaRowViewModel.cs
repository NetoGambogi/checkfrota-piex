using checkfrota_front.Models;
using checkfrota_front.Services;
using System.Globalization;

namespace checkfrota_front.ViewModels;

/// <summary>Linha de ocorrência nas listas do motorista e da gestão da frota.</summary>
public class OcorrenciaRowViewModel
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public OcorrenciaItem Original { get; }

    public string Titulo { get; }
    public string Subtitulo { get; }
    public string DetalheLabel { get; }
    public string Descricao { get; }
    public string GravidadeLabel { get; }

    public bool IsAlta { get; }
    public bool IsMedia { get; }
    public bool IsBaixa { get; }

    public bool IsResolvida { get; }

    /// <summary>Registrada no aparelho e ainda não confirmada pela API.</summary>
    public OperacaoPendente? Pendencia { get; }

    public string StatusLabel => Pendencia?.Situacao switch
    {
        SituacaoOperacao.Pendente => "Aguardando envio",
        SituacaoOperacao.Recusada => "Não enviada",
        _ => IsResolvida ? "Resolvida" : "Aberta",
    };

    public bool HasFoto { get; }

    public OcorrenciaRowViewModel(OcorrenciaItem item, OperacaoPendente? pendencia = null)
    {
        Original = item;
        Pendencia = pendencia;

        var placa = item.Veiculo?.Placa ?? "-";
        Titulo = $"{item.TipoLabel} · {placa}";

        var rota = item.Viagem?.Rota?.Nome;
        Subtitulo = item.Motorista?.Name is string motorista
            ? string.IsNullOrWhiteSpace(rota) ? motorista : $"{motorista} · {rota}"
            : rota ?? string.Empty;

        DetalheLabel = $"km {item.Km.ToString("N0", PtBr)} · {FormatarDataHora(item.CriadoEm)}";
        Descricao = item.Descricao;
        GravidadeLabel = item.GravidadeLabel;

        IsAlta = item.Gravidade == "alta";
        IsMedia = item.Gravidade == "media";
        IsBaixa = item.Gravidade == "baixa";

        IsResolvida = item.Status == "resolvida";
        HasFoto = item.Foto is not null;
    }

    public static string FormatarDataHora(string? isoData) =>
        DateTime.TryParse(isoData, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var data)
            ? data.ToLocalTime().ToString("dd/MM/yyyy HH:mm", PtBr)
            : "-";
}
