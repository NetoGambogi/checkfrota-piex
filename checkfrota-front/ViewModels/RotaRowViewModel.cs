using checkfrota_front.Models;
using System.Globalization;

namespace checkfrota_front.ViewModels;

public class RotaRowViewModel
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public RotaListItem Original { get; }

    public int Id { get; }
    public string Nome { get; }
    public string Trajeto { get; }
    public string KmLabel { get; }

    public RotaRowViewModel(RotaListItem item)
    {
        Original = item;
        Id = item.Id;
        Nome = item.Nome;
        Trajeto = $"{item.Origem} → {item.Destino}";
        KmLabel = $"~{item.KmAproximado.ToString("N0", PtBr)} km";
    }
}
