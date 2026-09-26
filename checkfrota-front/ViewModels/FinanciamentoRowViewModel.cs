using checkfrota_front.Models;
using System.Globalization;

namespace checkfrota_front.ViewModels;

public class FinanciamentoRowViewModel
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public FinanciamentoListItem Original { get; }

    public int Id { get; }
    public string Descricao { get; }
    public string CategoriaNome { get; }
    public string ValorParcelaFormatado { get; }
    public string ValorTotalFormatado { get; }
    public string ProgressoLabel { get; }

    public FinanciamentoRowViewModel(FinanciamentoListItem item)
    {
        Original = item;
        Id = item.Id;
        Descricao = item.Descricao;
        CategoriaNome = item.Categoria?.Nome ?? "-";
        ValorParcelaFormatado = item.ValorParcela.ToString("C2", PtBr);
        ValorTotalFormatado = item.ValorTotal.ToString("C2", PtBr);
        ProgressoLabel = $"{item.ParcelasPagas ?? 0}/{item.QuantidadeParcelas} parcelas pagas";
    }
}
