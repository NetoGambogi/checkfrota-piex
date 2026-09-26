using checkfrota_front.Helpers;
using checkfrota_front.Models;
using System.Globalization;

namespace checkfrota_front.ViewModels;

public class MovimentacaoFinanceiraRowViewModel
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public MovimentacaoFinanceiraListItem Original { get; }

    public int Id { get; }
    public string Descricao { get; }
    public string CategoriaNome { get; }
    public string Tipo { get; }
    public string Status { get; }

    public bool IsEntrada => Tipo == "entrada";
    public bool IsPago => Status == "pago";

    public string IconGlyph => IsEntrada ? IconFont.Entrada : IconFont.Saida;
    public string StatusLabel => IsPago ? "Pago" : "Pendente";
    public string ValorFormatado => Original.Valor.ToString("C2", PtBr);
    public string DataVencimentoFormatada => DateTime.TryParse(Original.DataVencimento, out var data)
        ? data.ToString("dd/MM/yyyy")
        : "-";

    public string? ParcelaLabel => Original.NumeroParcela is int numero ? $"Parcela {numero}" : null;
    public bool HasParcelaLabel => ParcelaLabel is not null;

    public MovimentacaoFinanceiraRowViewModel(MovimentacaoFinanceiraListItem item)
    {
        Original = item;
        Id = item.Id;
        Descricao = item.Descricao;
        CategoriaNome = item.Categoria?.Nome ?? "-";
        Tipo = item.Tipo;
        Status = item.Status;
    }
}
