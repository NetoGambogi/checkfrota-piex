using checkfrota_front.Helpers;
using checkfrota_front.Models;

namespace checkfrota_front.ViewModels;

public class CategoriaFinanceiraRowViewModel
{
    public CategoriaFinanceiraListItem Original { get; }

    public int Id { get; }
    public string Nome { get; }
    public string? Descricao { get; }
    public string Tipo { get; }
    public string Icone { get; }

    public string IconGlyph => Icone switch
    {
        "combustivel" => IconFont.CategoriaCombustivel,
        "manutencao" => IconFont.CategoriaManutencao,
        "pedagio" => IconFont.CategoriaPedagio,
        "seguro" => IconFont.CategoriaSeguro,
        _ => IconFont.CategoriaOutros,
    };

    public CategoriaFinanceiraRowViewModel(CategoriaFinanceiraListItem categoria)
    {
        Original = categoria;
        Id = categoria.Id;
        Nome = categoria.Nome;
        Descricao = categoria.Descricao;
        Tipo = categoria.Tipo;
        Icone = categoria.Icone;
    }
}
