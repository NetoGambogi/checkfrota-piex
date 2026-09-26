using checkfrota_front.Models;
using checkfrota_front.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;

namespace checkfrota_front.ViewModels;

public partial class MovimentacaoFinanceiraFormViewModel : AdminSectionViewModelBase, IQueryAttributable
{
    private static readonly FormaPagamentoListItem NenhumaForma = new() { Id = 0, Nome = "Nenhuma" };

    private readonly MovimentacaoFinanceiraService _movimentacaoService;
    private readonly CategoriaFinanceiraService _categoriaService;
    private readonly FormaPagamentoService _formaPagamentoService;

    private int? _movimentacaoId;
    private int? _categoriaIdParaSelecionar;
    private int? _formaPagamentoIdParaSelecionar;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageTitle))]
    [NotifyPropertyChangedFor(nameof(IsCreating))]
    [NotifyPropertyChangedFor(nameof(CanMarcarComoPago))]
    private bool isEditing;

    public string PageTitle => IsEditing ? "Editar lançamento" : "Novo lançamento";
    public bool IsCreating => !IsEditing;

    [ObservableProperty]
    private string tipo = "saida";

    public string MarcarComoPagoLabel => Tipo == "entrada" ? "Já foi recebido" : "Já foi pago";

    [ObservableProperty]
    private string descricao = string.Empty;

    [ObservableProperty]
    private string valor = string.Empty;

    [ObservableProperty]
    private DateTime dataVencimento = DateTime.Today;

    [ObservableProperty]
    private ObservableCollection<CategoriaFinanceiraListItem> categorias = new();

    [ObservableProperty]
    private CategoriaFinanceiraListItem? selectedCategoria;

    [ObservableProperty]
    private List<FormaPagamentoListItem> formasPagamento = new();

    [ObservableProperty]
    private FormaPagamentoListItem selectedFormaPagamento = NenhumaForma;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPendente))]
    [NotifyPropertyChangedFor(nameof(CanMarcarComoPago))]
    [NotifyPropertyChangedFor(nameof(StatusLabel))]
    private string status = "pendente";

    public bool IsPendente => Status == "pendente";
    public bool CanMarcarComoPago => IsEditing && IsPendente;
    public string StatusLabel => IsPendente ? "Pendente" : "Pago";

    [ObservableProperty]
    private bool marcarComoPago;

    [ObservableProperty]
    private DateTime dataPagamento = DateTime.Today;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasParcelaLabel))]
    private string? parcelaLabel;

    public bool HasParcelaLabel => ParcelaLabel is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private string? errorMessage;

    public MovimentacaoFinanceiraFormViewModel(
        MovimentacaoFinanceiraService movimentacaoService,
        CategoriaFinanceiraService categoriaService,
        FormaPagamentoService formaPagamentoService,
        ApiAuthService apiAuth) : base(apiAuth)
    {
        _movimentacaoService = movimentacaoService;
        _categoriaService = categoriaService;
        _formaPagamentoService = formaPagamentoService;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("movimentacao", out var obj) && obj is MovimentacaoFinanceiraListItem item)
        {
            _movimentacaoId = item.Id;
            Tipo = item.Tipo;
            Descricao = item.Descricao;
            Valor = item.Valor.ToString(CultureInfo.InvariantCulture);
            if (DateTime.TryParse(item.DataVencimento, out var venc)) DataVencimento = venc;
            Status = item.Status;
            DataPagamento = DateTime.TryParse(item.DataPagamento, out var pag) ? pag : DateTime.Today;
            ParcelaLabel = item.NumeroParcela is int numero ? $"Parcela {numero}" : null;
            _categoriaIdParaSelecionar = item.Categoria?.Id;
            _formaPagamentoIdParaSelecionar = item.FormaPagamento?.Id;
            IsEditing = true;
        }
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        await LoadAvatarAsync();
        await LoadFormasPagamentoAsync();
        await LoadCategoriasAsync();
    }

    private async Task LoadFormasPagamentoAsync()
    {
        var formas = await _formaPagamentoService.GetFormasAsync();
        var options = new List<FormaPagamentoListItem> { NenhumaForma };
        options.AddRange(formas);
        FormasPagamento = options;

        SelectedFormaPagamento = _formaPagamentoIdParaSelecionar is int formaId
            ? FormasPagamento.FirstOrDefault(f => f.Id == formaId) ?? NenhumaForma
            : NenhumaForma;
    }

    private async Task LoadCategoriasAsync()
    {
        var tipoCategoria = Tipo == "entrada" ? "receita" : "despesa";
        var categoriasCarregadas = await _categoriaService.GetCategoriasAsync(tipoCategoria);
        Categorias = new ObservableCollection<CategoriaFinanceiraListItem>(categoriasCarregadas);

        SelectedCategoria = _categoriaIdParaSelecionar is int categoriaId
            ? Categorias.FirstOrDefault(c => c.Id == categoriaId)
            : Categorias.FirstOrDefault();

        _categoriaIdParaSelecionar = null;
    }

    [RelayCommand]
    private async Task SelectEntradaAsync()
    {
        Tipo = "entrada";
        await LoadCategoriasAsync();
    }

    [RelayCommand]
    private async Task SelectSaidaAsync()
    {
        Tipo = "saida";
        await LoadCategoriasAsync();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;

        if (string.IsNullOrWhiteSpace(Descricao))
        {
            ErrorMessage = "Informe a descrição do lançamento.";
            return;
        }

        if (!decimal.TryParse(Valor, NumberStyles.Number, CultureInfo.InvariantCulture, out var valorDecimal) || valorDecimal <= 0)
        {
            ErrorMessage = "Informe um valor válido.";
            return;
        }

        if (SelectedCategoria is null)
        {
            ErrorMessage = "Selecione uma categoria.";
            return;
        }

        if (!IsEditing && MarcarComoPago && SelectedFormaPagamento.Id == 0)
        {
            ErrorMessage = "Selecione a forma de pagamento para lançar como pago.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var formaPagamentoId = SelectedFormaPagamento.Id > 0 ? SelectedFormaPagamento.Id : (int?)null;

            var resultado = _movimentacaoId is int id
                ? await _movimentacaoService.UpdateAsync(id, Tipo, Descricao, valorDecimal, DataVencimento, SelectedCategoria.Id, formaPagamentoId)
                : await _movimentacaoService.CreateAsync(
                    Tipo,
                    Descricao,
                    valorDecimal,
                    DataVencimento,
                    SelectedCategoria.Id,
                    formaPagamentoId,
                    MarcarComoPago ? "pago" : "pendente",
                    MarcarComoPago ? DataPagamento : null);

            if (resultado is null)
            {
                ErrorMessage = "Não foi possível salvar o lançamento.";
                return;
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado ao salvar o lançamento.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task MarcarComoPagoAsync()
    {
        if (IsBusy || _movimentacaoId is not int id) return;

        if (SelectedFormaPagamento.Id == 0)
        {
            ErrorMessage = "Selecione a forma de pagamento antes de marcar como pago.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var resultado = await _movimentacaoService.PagarAsync(id, SelectedFormaPagamento.Id, DataPagamento);
            if (resultado is null)
            {
                ErrorMessage = "Não foi possível marcar o lançamento como pago.";
                return;
            }

            Status = resultado.Status;
            if (DateTime.TryParse(resultado.DataPagamento, out var pag)) DataPagamento = pag;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado ao marcar como pago.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (IsBusy || _movimentacaoId is not int id) return;

        var confirmar = await Shell.Current.DisplayAlertAsync(
            "Excluir lançamento",
            $"Tem certeza que deseja excluir \"{Descricao}\"?",
            "Excluir",
            "Cancelar");

        if (!confirmar) return;

        try
        {
            IsBusy = true;
            var ok = await _movimentacaoService.DeleteAsync(id);
            if (!ok)
            {
                ErrorMessage = "Não foi possível excluir esse lançamento.";
                return;
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado ao excluir o lançamento.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
