using checkfrota_front.Models;
using System.Globalization;

namespace checkfrota_front.ViewModels;

public class ViagemRowViewModel
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public ViagemListItem Original { get; }

    public int Id { get; }
    public string RotaNome { get; }
    public string Trajeto { get; }
    public string Subtitulo { get; }
    public string PeriodoLabel { get; }
    public string KmLabel { get; }
    public bool EmAndamento { get; }

    /// <summary>Motorista a quem a viagem foi atribuída; vazio em viagens antigas sem motorista.</summary>
    public string MotoristaNome { get; }
    public bool HasMotorista => !string.IsNullOrEmpty(MotoristaNome);

    public bool HasFrete { get; }
    public string FreteFormatado { get; }
    public bool IsRecebido { get; }
    public string FreteStatusLabel => IsRecebido ? "Recebido" : "A receber";

    /// <summary>Texto usado na busca local: rota, trajeto, placa, cliente e motorista.</summary>
    public string TextoBusca { get; }

    public ViagemRowViewModel(ViagemListItem item)
    {
        Original = item;
        Id = item.Id;
        RotaNome = item.Rota?.Nome ?? "Rota";
        Trajeto = item.Rota is { } rota ? $"{rota.Origem} → {rota.Destino}" : string.Empty;

        MotoristaNome = item.Motorista?.Name ?? string.Empty;

        var placa = item.Veiculo?.Placa ?? "-";
        Subtitulo = string.IsNullOrWhiteSpace(item.Cliente) ? placa : $"{placa} · {item.Cliente}";

        EmAndamento = item.Status != "concluida";
        var saida = FormatarData(item.DataSaida);
        PeriodoLabel = EmAndamento
            ? $"Saída {saida} · em andamento"
            : $"{saida} → {FormatarData(item.DataChegada)}";

        KmLabel = item.KmPercorrido is int percorrido
            ? $"{percorrido.ToString("N0", PtBr)} km"
            : item.KmEstimado is int estimado ? $"~{estimado.ToString("N0", PtBr)} km" : string.Empty;

        HasFrete = item.ValorFrete is not null;
        FreteFormatado = item.ValorFrete?.ToString("C2", PtBr) ?? string.Empty;
        IsRecebido = item.Financeiro?.IsPago == true;

        TextoBusca = $"{item.Rota?.Nome} {item.Rota?.Origem} {item.Rota?.Destino} {placa} {item.Cliente} {MotoristaNome}";
    }

    private static string FormatarData(string? data) =>
        DateTime.TryParse(data, out var valor) ? valor.ToString("dd/MM/yyyy", PtBr) : "-";
}
