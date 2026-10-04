using checkfrota_front.Helpers;
using checkfrota_front.Models;
using checkfrota_front.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;

namespace checkfrota_front.ViewModels;

public class CombustivelOption
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public partial class AbastecimentoFormViewModel : AdminSectionViewModelBase, IQueryAttributable
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static List<CombustivelOption> CombustivelOptions { get; } = new()
    {
        new CombustivelOption { Key = "diesel_s10", Label = "Diesel S10" },
        new CombustivelOption { Key = "diesel_s500", Label = "Diesel S500" },
        new CombustivelOption { Key = "gasolina", Label = "Gasolina" },
        new CombustivelOption { Key = "etanol", Label = "Etanol" },
        new CombustivelOption { Key = "gnv", Label = "GNV" },
    };

    private readonly AbastecimentoService _abastecimentoService;
    private readonly VeiculoService _veiculoService;

    private AbastecimentoListItem? _original;
    private int? _veiculoIdInicial;
    private bool _opcoesCarregadas;

    public LancamentoFinanceiroSection Financeiro { get; }

    public List<CombustivelOption> Combustiveis => CombustivelOptions;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageTitle))]
    [NotifyPropertyChangedFor(nameof(IsCreating))]
    private bool isEditing;

    public bool IsCreating => !IsEditing;

    public string PageTitle => IsEditing ? "Editar abastecimento" : "Novo abastecimento";

    [ObservableProperty]
    private ObservableCollection<VeiculoListItem> veiculos = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UltimoKmLabel))]
    private VeiculoListItem? selectedVeiculo;

    public string? UltimoKmLabel => IsCreating && SelectedVeiculo is not null
        ? $"Último km registrado: {SelectedVeiculo.KmAtual.ToString("N0", PtBr)} km"
        : null;

    [ObservableProperty]
    private DateTime data = DateTime.Today;

    [ObservableProperty]
    private string km = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PrecoLitroLabel))]
    private string litros = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PrecoLitroLabel))]
    private string valorTotal = string.Empty;

    /// <summary>Preço por litro calculado a partir do que foi digitado, ex: "R$ 6,190/L".</summary>
    public string? PrecoLitroLabel =>
        FormParsing.TryParseDecimal(Litros, out var l) && l > 0 && FormParsing.TryParseValor(ValorTotal, out var v)
            ? $"Preço por litro: {(v / l).ToString("C3", PtBr)}"
            : null;

    [ObservableProperty]
    private CombustivelOption selectedCombustivel = CombustivelOptions[0];

    [ObservableProperty]
    private bool tanqueCheio = true;

    [ObservableProperty]
    private string? posto;

    [ObservableProperty]
    private string? statusFinanceiroLabel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private string? errorMessage;

    public AbastecimentoFormViewModel(
        AbastecimentoService abastecimentoService,
        VeiculoService veiculoService,
        LancamentoFinanceiroSection financeiro,
        ApiAuthService apiAuth) : base(apiAuth)
    {
        _abastecimentoService = abastecimentoService;
        _veiculoService = veiculoService;
        Financeiro = financeiro;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("abastecimento", out var abastecimentoObj) && abastecimentoObj is AbastecimentoListItem item)
        {
            _original = item;
            if (DateTime.TryParse(item.Data, out var dataAbastecimento)) Data = dataAbastecimento;
            Km = item.Km.ToString();
            Litros = item.Litros.ToString("0.###", PtBr);
            ValorTotal = item.ValorTotal.ToString("0.00", PtBr);
            SelectedCombustivel = CombustivelOptions.FirstOrDefault(c => c.Key == item.Combustivel) ?? CombustivelOptions[0];
            TanqueCheio = item.TanqueCheio;
            Posto = item.Posto;

            StatusFinanceiroLabel = item.Financeiro?.IsPago == true
                ? "Despesa já paga no financeiro. Alterações de valor também atualizam o lançamento."
                : "Despesa pendente no financeiro. O pagamento é registrado pelo financeiro.";

            IsEditing = true;
        }
        else if (query.TryGetValue("veiculoId", out var veiculoIdObj) && veiculoIdObj is int veiculoId)
        {
            _veiculoIdInicial = veiculoId;
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

            var veiculoId = _original?.Veiculo?.Id ?? _veiculoIdInicial;
            SelectedVeiculo = veiculoId is int id
                ? Veiculos.FirstOrDefault(v => v.Id == id)
                : Veiculos.Count == 1 ? Veiculos[0] : null;

            await Financeiro.LoadAsync("despesa", "combustivel", _original?.Financeiro);
            ErrorMessage = Financeiro.Categorias.Count == 0 ? Financeiro.Validar() : null;

            _opcoesCarregadas = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível carregar veículos e categorias.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;

        if (SelectedVeiculo is null)
        {
            ErrorMessage = "Selecione o veículo.";
            return;
        }

        if (!FormParsing.TryParseKm(Km, out var kmInt))
        {
            ErrorMessage = "Informe o km do hodômetro no abastecimento.";
            return;
        }

        if (!FormParsing.TryParseDecimal(Litros, out var litrosDecimal) || litrosDecimal <= 0)
        {
            ErrorMessage = "Informe a quantidade de litros.";
            return;
        }

        if (!FormParsing.TryParseValor(ValorTotal, out var valorDecimal))
        {
            ErrorMessage = "Informe o valor total do abastecimento.";
            return;
        }

        if (Financeiro.Validar() is string erroFinanceiro)
        {
            ErrorMessage = erroFinanceiro;
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var resultado = await _abastecimentoService.SaveAsync(
                _original?.Id,
                SelectedVeiculo.Id,
                Data,
                kmInt,
                litrosDecimal,
                valorDecimal,
                SelectedCombustivel.Key,
                TanqueCheio,
                string.IsNullOrWhiteSpace(Posto) ? null : Posto.Trim(),
                Financeiro.ToDados(Data));

            if (!resultado.Sucesso)
            {
                ErrorMessage = resultado.Erro ?? "Não foi possível salvar o abastecimento.";
                return;
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado ao salvar o abastecimento.";
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

        var mensagem = _original.Financeiro?.IsPago == true
            ? "A despesa já foi paga e continuará no financeiro."
            : "A despesa pendente lançada no financeiro também será excluída.";

        var confirmar = await Shell.Current.DisplayAlertAsync(
            "Excluir abastecimento",
            $"Tem certeza que deseja excluir este abastecimento? {mensagem} As médias de consumo serão recalculadas.",
            "Excluir",
            "Cancelar");

        if (!confirmar) return;

        try
        {
            IsBusy = true;
            var ok = await _abastecimentoService.DeleteAsync(_original.Id);
            if (!ok)
            {
                ErrorMessage = "Não foi possível excluir esse abastecimento.";
                return;
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado ao excluir o abastecimento.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
