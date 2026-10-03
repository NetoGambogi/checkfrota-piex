using checkfrota_front.Models;
using System.Globalization;

namespace checkfrota_front.ViewModels;

public class ManutencaoRowViewModel
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public ManutencaoListItem Original { get; }

    public int Id { get; }
    public string Descricao { get; }
    public string VeiculoLabel { get; }
    public string DataLabel { get; }
    public string ValorFormatado { get; }
    public bool IsPago { get; }
    public string StatusLabel => IsPago ? "Pago" : "Pendente";

    /// <summary>Texto usado na busca local: descrição, placa e oficina.</summary>
    public string TextoBusca { get; }

    public ManutencaoRowViewModel(ManutencaoListItem item)
    {
        Original = item;
        Id = item.Id;
        Descricao = item.Descricao;

        var tipo = item.Tipo == "corretiva" ? "Corretiva" : "Preventiva";
        VeiculoLabel = item.Veiculo is null ? tipo : $"{item.Veiculo.Placa} · {tipo}";

        DataLabel = DateTime.TryParse(item.Data, out var data) ? data.ToString("dd/MM/yyyy", PtBr) : "-";
        if (item.Km is int km) DataLabel += $" · {km.ToString("N0", PtBr)} km";

        ValorFormatado = item.Valor.ToString("C2", PtBr);
        IsPago = item.Financeiro?.Status == "pago";
        TextoBusca = $"{item.Descricao} {item.Veiculo?.Placa} {item.Oficina}";
    }
}
