using checkfrota_front.Models;

namespace checkfrota_front.ViewModels;

public class FormaPagamentoRowViewModel
{
    public FormaPagamentoListItem Original { get; }

    public int Id { get; }
    public string Nome { get; }

    public FormaPagamentoRowViewModel(FormaPagamentoListItem forma)
    {
        Original = forma;
        Id = forma.Id;
        Nome = forma.Nome;
    }
}
