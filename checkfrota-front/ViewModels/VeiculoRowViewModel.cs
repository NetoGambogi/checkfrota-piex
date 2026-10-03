using checkfrota_front.Models;
using System.Globalization;

namespace checkfrota_front.ViewModels;

public class VeiculoRowViewModel
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public VeiculoListItem Original { get; }

    public int Id { get; }
    public string Placa { get; }
    public string Descricao { get; }
    public string KmLabel { get; }
    public string TotalManutencoesFormatado { get; }

    public VeiculoRowViewModel(VeiculoListItem item)
    {
        Original = item;
        Id = item.Id;
        Placa = item.Placa;

        var tipo = VeiculoFormViewModel.TipoOptions.FirstOrDefault(t => t.Key == item.Tipo)?.Label ?? item.Tipo;
        Descricao = item.Ano is int ano
            ? $"{item.Marca} {item.Modelo} {ano} · {tipo}"
            : $"{item.Marca} {item.Modelo} · {tipo}";

        KmLabel = $"{item.KmAtual.ToString("N0", PtBr)} km";
        TotalManutencoesFormatado = (item.TotalManutencoes ?? 0).ToString("C2", PtBr);
    }
}
