using checkfrota_front.Models;
using checkfrota_front.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace checkfrota_front.ViewModels;

/// <summary>
/// Campos do lançamento financeiro (categoria, forma de pagamento, pago/recebido e vencimento)
/// usados pelos formulários da frota que geram despesa ou receita. Exibido pela view
/// <c>Views/Shared/LancamentoFinanceiroFields</c>.
/// </summary>
public partial class LancamentoFinanceiroSection : ObservableObject
{
    private static readonly FormaPagamentoListItem NenhumaForma = new() { Id = 0, Nome = "Nenhuma" };

    private readonly CategoriaFinanceiraService _categoriaService;
    private readonly FormaPagamentoService _formaPagamentoService;

    private string _tipoCategoria = "despesa";

    [ObservableProperty]
    private ObservableCollection<CategoriaFinanceiraListItem> categorias = new();

    [ObservableProperty]
    private CategoriaFinanceiraListItem? selectedCategoria;

    [ObservableProperty]
    private List<FormaPagamentoListItem> formasPagamento = new();

    [ObservableProperty]
    private FormaPagamentoListItem selectedFormaPagamento = NenhumaForma;

    /// <summary>Só pode ser marcado ao criar o lançamento; depois, o pagamento é feito pelo financeiro.</summary>
    [ObservableProperty]
    private bool permiteMarcarComoPago = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDataVencimento))]
    private bool marcarComoPago;

    [ObservableProperty]
    private DateTime dataVencimento = DateTime.Today;

    [ObservableProperty]
    private string tituloCategoria = "Categoria da despesa";

    [ObservableProperty]
    private string textoMarcarComoPago = "Já foi paga";

    [ObservableProperty]
    private string textoDataVencimento = "Vencimento da despesa";

    /// <summary>Lançamento já pago é registrado na data do próprio registro; não há vencimento a escolher.</summary>
    public bool ShowDataVencimento => !(MarcarComoPago && PermiteMarcarComoPago);

    // Status do lançamento existente (edição), que não muda por aqui.
    private bool _existentePago;

    /// <summary>
    /// O lançamento está pago: ao criar, pelo "Já foi paga" marcado; ao editar, pelo status salvo.
    /// Pagamentos exigem o comprovante.
    /// </summary>
    public bool IsPago => PermiteMarcarComoPago ? MarcarComoPago : _existentePago;

    partial void OnPermiteMarcarComoPagoChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowDataVencimento));
        OnPropertyChanged(nameof(IsPago));
    }

    partial void OnMarcarComoPagoChanged(bool value) => OnPropertyChanged(nameof(IsPago));

    public LancamentoFinanceiroSection(CategoriaFinanceiraService categoriaService, FormaPagamentoService formaPagamentoService)
    {
        _categoriaService = categoriaService;
        _formaPagamentoService = formaPagamentoService;
    }

    /// <summary>
    /// Carrega categorias do tipo informado e formas de pagamento. Com um lançamento existente,
    /// seleciona os valores dele; senão sugere a categoria com <paramref name="iconeSugerido"/>.
    /// </summary>
    public async Task LoadAsync(string tipoCategoria, string? iconeSugerido, MovimentacaoVinculada? existente)
    {
        _tipoCategoria = tipoCategoria;

        if (tipoCategoria == "receita")
        {
            TituloCategoria = "Categoria da receita";
            TextoMarcarComoPago = "Já foi recebido";
            TextoDataVencimento = "Previsão de recebimento";
        }

        var categoriasDoTipo = await _categoriaService.GetCategoriasAsync(tipoCategoria);
        Categorias = new ObservableCollection<CategoriaFinanceiraListItem>(categoriasDoTipo);

        var formas = await _formaPagamentoService.GetFormasAsync();
        var options = new List<FormaPagamentoListItem> { NenhumaForma };
        options.AddRange(formas);
        FormasPagamento = options;

        if (existente is not null)
        {
            SelectedCategoria = Categorias.FirstOrDefault(c => c.Id == existente.Categoria?.Id);
            SelectedFormaPagamento = FormasPagamento.FirstOrDefault(f => f.Id == existente.FormaPagamento?.Id) ?? NenhumaForma;
            if (DateTime.TryParse(existente.DataVencimento, out var vencimento)) DataVencimento = vencimento;
            _existentePago = existente.IsPago;
            PermiteMarcarComoPago = false;
        }
        else
        {
            SelectedCategoria = Categorias.FirstOrDefault(c => c.Icone == iconeSugerido) ?? Categorias.FirstOrDefault();
            SelectedFormaPagamento = NenhumaForma;
        }
    }

    /// <summary>Mensagem de erro se os campos estiverem incompletos; null se estiverem válidos.</summary>
    public string? Validar()
    {
        if (Categorias.Count == 0)
        {
            return $"Nenhuma categoria de {_tipoCategoria} cadastrada. Peça ao financeiro para cadastrar uma.";
        }

        if (SelectedCategoria is null)
        {
            return $"Selecione a categoria da {_tipoCategoria}.";
        }

        if (PermiteMarcarComoPago && MarcarComoPago && SelectedFormaPagamento.Id == 0)
        {
            return "Informe a forma de pagamento para lançamentos já pagos.";
        }

        return null;
    }

    /// <param name="dataReferencia">Data do registro, usada como vencimento quando já está pago.</param>
    public LancamentoFinanceiroDados ToDados(DateTime dataReferencia)
    {
        var pago = PermiteMarcarComoPago && MarcarComoPago;

        return new LancamentoFinanceiroDados(
            SelectedCategoria!.Id,
            SelectedFormaPagamento.Id > 0 ? SelectedFormaPagamento.Id : null,
            pago ? dataReferencia : DataVencimento,
            pago ? "pago" : "pendente");
    }
}
