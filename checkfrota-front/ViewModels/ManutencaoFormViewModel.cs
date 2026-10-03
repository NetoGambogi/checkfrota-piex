using checkfrota_front.Models;
using checkfrota_front.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;

namespace checkfrota_front.ViewModels;

public partial class ManutencaoFormViewModel : AdminSectionViewModelBase, IQueryAttributable
{
    private static readonly FormaPagamentoListItem NenhumaForma = new() { Id = 0, Nome = "Nenhuma" };

    private readonly ManutencaoService _manutencaoService;
    private readonly VeiculoService _veiculoService;
    private readonly CategoriaFinanceiraService _categoriaService;
    private readonly FormaPagamentoService _formaPagamentoService;

    private ManutencaoListItem? _original;
    private bool _opcoesCarregadas;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageTitle))]
    [NotifyPropertyChangedFor(nameof(IsCreating))]
    private bool isEditing;

    public bool IsCreating => !IsEditing;

    public string PageTitle => IsEditing ? "Editar manutenção" : "Nova manutenção";

    [ObservableProperty]
    private ObservableCollection<VeiculoListItem> veiculos = new();

    [ObservableProperty]
    private VeiculoListItem? selectedVeiculo;

    [ObservableProperty]
    private string tipo = "preventiva";

    [ObservableProperty]
    private string descricao = string.Empty;

    [ObservableProperty]
    private DateTime data = DateTime.Today;

    [ObservableProperty]
    private string km = string.Empty;

    [ObservableProperty]
    private string? oficina;

    [ObservableProperty]
    private string valor = string.Empty;

    [ObservableProperty]
    private ObservableCollection<CategoriaFinanceiraListItem> categorias = new();

    [ObservableProperty]
    private CategoriaFinanceiraListItem? selectedCategoria;

    [ObservableProperty]
    private List<FormaPagamentoListItem> formasPagamento = new();

    [ObservableProperty]
    private FormaPagamentoListItem selectedFormaPagamento = NenhumaForma;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDataVencimento))]
    private bool marcarComoPago;

    [ObservableProperty]
    private DateTime dataVencimento = DateTime.Today;

    /// <summary>Manutenção já paga é lançada com pagamento na própria data; não há vencimento a escolher.</summary>
    public bool ShowDataVencimento => !MarcarComoPago;

    [ObservableProperty]
    private string? statusFinanceiroLabel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private string? errorMessage;

    public ManutencaoFormViewModel(
        ManutencaoService manutencaoService,
        VeiculoService veiculoService,
        CategoriaFinanceiraService categoriaService,
        FormaPagamentoService formaPagamentoService,
        ApiAuthService apiAuth) : base(apiAuth)
    {
        _manutencaoService = manutencaoService;
        _veiculoService = veiculoService;
        _categoriaService = categoriaService;
        _formaPagamentoService = formaPagamentoService;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("manutencao", out var manutencaoObj) && manutencaoObj is ManutencaoListItem item)
        {
            _original = item;
            Tipo = item.Tipo;
            Descricao = item.Descricao;
            if (DateTime.TryParse(item.Data, out var dataManutencao)) Data = dataManutencao;
            Km = item.Km?.ToString() ?? string.Empty;
            Oficina = item.Oficina;
            Valor = item.Valor.ToString("0.00", CultureInfo.GetCultureInfo("pt-BR"));
            if (DateTime.TryParse(item.Financeiro?.DataVencimento, out var vencimento)) DataVencimento = vencimento;

            StatusFinanceiroLabel = item.Financeiro?.Status == "pago"
                ? "Despesa já paga no financeiro. Alterações de valor também atualizam o lançamento."
                : "Despesa pendente no financeiro. O pagamento é registrado pelo financeiro.";

            IsEditing = true;
        }
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        await LoadAvatarAsync();

        if (_opcoesCarregadas) return;

        try
        {
            var veiculosAtivos = await _veiculoService.GetVeiculosAsync();
            Veiculos = new ObservableCollection<VeiculoListItem>(veiculosAtivos);

            var categoriasDespesa = await _categoriaService.GetCategoriasAsync("despesa");
            Categorias = new ObservableCollection<CategoriaFinanceiraListItem>(categoriasDespesa);

            var formas = await _formaPagamentoService.GetFormasAsync();
            var options = new List<FormaPagamentoListItem> { NenhumaForma };
            options.AddRange(formas);
            FormasPagamento = options;

            if (_original is not null)
            {
                SelectedVeiculo = Veiculos.FirstOrDefault(v => v.Id == _original.Veiculo?.Id);
                SelectedCategoria = Categorias.FirstOrDefault(c => c.Id == _original.Financeiro?.Categoria?.Id);
                SelectedFormaPagamento = FormasPagamento.FirstOrDefault(f => f.Id == _original.Financeiro?.FormaPagamento?.Id) ?? NenhumaForma;
            }
            else
            {
                SelectedVeiculo = Veiculos.Count == 1 ? Veiculos[0] : null;
                // Sugere a categoria de manutenção, se existir uma cadastrada com esse ícone.
                SelectedCategoria = Categorias.FirstOrDefault(c => c.Icone == "manutencao") ?? Categorias.FirstOrDefault();
                SelectedFormaPagamento = NenhumaForma;
            }

            if (Categorias.Count == 0)
            {
                ErrorMessage = "Nenhuma categoria de despesa cadastrada. Peça ao financeiro para cadastrar uma (ex: Manutenção).";
            }

            _opcoesCarregadas = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível carregar veículos e categorias.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    [RelayCommand]
    private void SelectPreventiva() => Tipo = "preventiva";

    [RelayCommand]
    private void SelectCorretiva() => Tipo = "corretiva";

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;

        if (SelectedVeiculo is null)
        {
            ErrorMessage = "Selecione o veículo.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Descricao))
        {
            ErrorMessage = "Informe o que foi feito na manutenção.";
            return;
        }

        if (!TryParseValor(out var valorDecimal))
        {
            ErrorMessage = "Informe um valor válido.";
            return;
        }

        int? kmInt = null;
        if (!string.IsNullOrWhiteSpace(Km))
        {
            if (!int.TryParse(Km.Replace(".", ""), out var kmValor) || kmValor < 0)
            {
                ErrorMessage = "Informe uma quilometragem válida.";
                return;
            }
            kmInt = kmValor;
        }

        if (SelectedCategoria is null)
        {
            ErrorMessage = "Selecione a categoria da despesa.";
            return;
        }

        var formaPagamentoId = SelectedFormaPagamento.Id > 0 ? SelectedFormaPagamento.Id : (int?)null;

        if (IsCreating && MarcarComoPago && formaPagamentoId is null)
        {
            ErrorMessage = "Informe a forma de pagamento para manutenções já pagas.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var resultado = await _manutencaoService.SaveAsync(
                _original?.Id,
                SelectedVeiculo.Id,
                Tipo,
                Descricao.Trim(),
                Data,
                kmInt,
                string.IsNullOrWhiteSpace(Oficina) ? null : Oficina.Trim(),
                valorDecimal,
                SelectedCategoria.Id,
                formaPagamentoId,
                MarcarComoPago ? Data : DataVencimento,
                MarcarComoPago ? "pago" : "pendente");

            if (!resultado.Sucesso)
            {
                ErrorMessage = resultado.Erro ?? "Não foi possível salvar a manutenção.";
                return;
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado ao salvar a manutenção.";
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
        if (IsBusy || _original is null) return;

        var mensagem = _original.Financeiro?.Status == "pago"
            ? "A despesa já foi paga e continuará no financeiro."
            : "A despesa pendente lançada no financeiro também será excluída.";

        var confirmar = await Shell.Current.DisplayAlertAsync(
            "Excluir manutenção",
            $"Tem certeza que deseja excluir \"{Descricao}\"? {mensagem}",
            "Excluir",
            "Cancelar");

        if (!confirmar) return;

        try
        {
            IsBusy = true;
            var ok = await _manutencaoService.DeleteAsync(_original.Id);
            if (!ok)
            {
                ErrorMessage = "Não foi possível excluir essa manutenção.";
                return;
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado ao excluir a manutenção.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool TryParseValor(out decimal valorDecimal)
    {
        var texto = Valor.Trim().Replace(",", ".");
        return decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out valorDecimal) && valorDecimal > 0;
    }
}
