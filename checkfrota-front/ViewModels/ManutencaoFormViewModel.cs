using checkfrota_front.Helpers;
using checkfrota_front.Models;
using checkfrota_front.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;

namespace checkfrota_front.ViewModels;

public partial class ManutencaoFormViewModel : AdminSectionViewModelBase, IQueryAttributable
{
    private readonly ManutencaoService _manutencaoService;
    private readonly VeiculoService _veiculoService;

    private ManutencaoListItem? _original;
    private bool _opcoesCarregadas;

    public LancamentoFinanceiroSection Financeiro { get; }

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
    private string? statusFinanceiroLabel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private string? errorMessage;

    /// <summary>Nota da oficina anexada ao lançamento da despesa, para o financeiro conferir.</summary>
    public ComprovanteSection ComprovanteLancamento { get; }

    /// <summary>
    /// Só para despesas já pagas, em que ele é obrigatório: ao lançar com "Já foi paga" marcado ou ao
    /// editar uma manutenção cuja despesa está paga.
    /// </summary>
    public bool ShowComprovanteLancamento => Financeiro.IsPago;

    public ManutencaoFormViewModel(
        ManutencaoService manutencaoService,
        VeiculoService veiculoService,
        LancamentoFinanceiroSection financeiro,
        ComprovanteSection comprovanteLancamento,
        ApiAuthService apiAuth) : base(apiAuth)
    {
        _manutencaoService = manutencaoService;
        _veiculoService = veiculoService;
        Financeiro = financeiro;
        ComprovanteLancamento = comprovanteLancamento;

        Financeiro.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LancamentoFinanceiroSection.IsPago))
                OnPropertyChanged(nameof(ShowComprovanteLancamento));
        };
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
            ComprovanteLancamento.Carregar(item.Financeiro?.Comprovante);

            StatusFinanceiroLabel = item.Financeiro?.IsPago == true
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
            BusyMessage = "Carregando…";
            IsBusy = true;

            var veiculosAtivos = await _veiculoService.GetVeiculosAsync();
            Veiculos = new ObservableCollection<VeiculoListItem>(veiculosAtivos);
            SelectedVeiculo = _original is not null
                ? Veiculos.FirstOrDefault(v => v.Id == _original.Veiculo?.Id)
                : Veiculos.Count == 1 ? Veiculos[0] : null;

            await Financeiro.LoadAsync("despesa", "manutencao", _original?.Financeiro);
            ErrorMessage = Financeiro.Categorias.Count == 0 ? Financeiro.Validar() : null;

            _opcoesCarregadas = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível carregar veículos e categorias.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
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

        if (!FormParsing.TryParseValor(Valor, out var valorDecimal))
        {
            ErrorMessage = "Informe um valor válido.";
            return;
        }

        if (!FormParsing.TryParseKmOpcional(Km, out var kmInt))
        {
            ErrorMessage = "Informe uma quilometragem válida.";
            return;
        }

        if (Financeiro.Validar() is string erroFinanceiro)
        {
            ErrorMessage = erroFinanceiro;
            return;
        }

        if (ComprovanteLancamento.Validar(obrigatorio: IsCreating && ShowComprovanteLancamento) is string erroComprovante)
        {
            ErrorMessage = erroComprovante;
            return;
        }

        try
        {
            BusyMessage = "Salvando…";
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
                Financeiro.ToDados(Data),
                // Comprovante escolhido e depois desmarcado o "Já foi paga" não vai junto.
                ShowComprovanteLancamento ? ComprovanteLancamento.Pendente : null);

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

        var mensagem = _original.Financeiro?.IsPago == true
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
            BusyMessage = "Excluindo…";
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
}
