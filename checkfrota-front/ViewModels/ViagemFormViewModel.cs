using checkfrota_front.Helpers;
using checkfrota_front.Models;
using checkfrota_front.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;

namespace checkfrota_front.ViewModels;

public partial class ViagemFormViewModel : AdminSectionViewModelBase, IQueryAttributable
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private readonly ViagemService _viagemService;
    private readonly RotaService _rotaService;
    private readonly VeiculoService _veiculoService;

    private ViagemListItem? _original;
    private bool _opcoesCarregadas;

    public LancamentoFinanceiroSection Financeiro { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageTitle))]
    [NotifyPropertyChangedFor(nameof(IsCreating))]
    private bool isEditing;

    public bool IsCreating => !IsEditing;

    public string PageTitle => IsEditing ? "Editar viagem" : "Nova viagem";

    [ObservableProperty]
    private ObservableCollection<RotaListItem> rotas = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KmEstimadoLabel))]
    private RotaListItem? selectedRota;

    public string? KmEstimadoLabel => SelectedRota is null
        ? null
        : $"{SelectedRota.Origem} → {SelectedRota.Destino} · ~{SelectedRota.KmAproximado.ToString("N0", PtBr)} km";

    [ObservableProperty]
    private ObservableCollection<VeiculoListItem> veiculos = new();

    [ObservableProperty]
    private VeiculoListItem? selectedVeiculo;

    [ObservableProperty]
    private DateTime dataSaida = DateTime.Today;

    [ObservableProperty]
    private bool concluida;

    [ObservableProperty]
    private DateTime dataChegada = DateTime.Today;

    [ObservableProperty]
    private string kmSaida = string.Empty;

    [ObservableProperty]
    private string kmChegada = string.Empty;

    [ObservableProperty]
    private string? cliente;

    [ObservableProperty]
    private string? observacoes;

    [ObservableProperty]
    private bool lancarFrete;

    [ObservableProperty]
    private string valorFrete = string.Empty;

    [ObservableProperty]
    private string? statusReceitaLabel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private string? errorMessage;

    public ViagemFormViewModel(
        ViagemService viagemService,
        RotaService rotaService,
        VeiculoService veiculoService,
        LancamentoFinanceiroSection financeiro,
        ApiAuthService apiAuth) : base(apiAuth)
    {
        _viagemService = viagemService;
        _rotaService = rotaService;
        _veiculoService = veiculoService;
        Financeiro = financeiro;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("viagem", out var viagemObj) && viagemObj is ViagemListItem item)
        {
            _original = item;
            if (DateTime.TryParse(item.DataSaida, out var saida)) DataSaida = saida;
            Concluida = item.DataChegada is not null;
            DataChegada = DateTime.TryParse(item.DataChegada, out var chegada) ? chegada : DateTime.Today;
            KmSaida = item.KmSaida?.ToString() ?? string.Empty;
            KmChegada = item.KmChegada?.ToString() ?? string.Empty;
            Cliente = item.Cliente;
            Observacoes = item.Observacoes;
            LancarFrete = item.ValorFrete is not null;
            ValorFrete = item.ValorFrete?.ToString("0.00", PtBr) ?? string.Empty;

            if (item.Financeiro is not null)
            {
                StatusReceitaLabel = item.Financeiro.IsPago
                    ? "Frete já recebido no financeiro. Alterações de valor também atualizam o lançamento."
                    : "Receita pendente no financeiro. O recebimento é registrado pelo financeiro.";
            }

            IsEditing = true;
        }
    }

    /// <summary>Ao escolher o veículo numa viagem nova, sugere o km atual dele como km de saída.</summary>
    partial void OnSelectedVeiculoChanged(VeiculoListItem? value)
    {
        if (IsCreating && value is not null && string.IsNullOrWhiteSpace(KmSaida))
        {
            KmSaida = value.KmAtual.ToString();
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

            var rotasAtivas = await _rotaService.GetRotasAsync();
            Rotas = new ObservableCollection<RotaListItem>(rotasAtivas);
            SelectedRota = Rotas.FirstOrDefault(r => r.Id == _original?.Rota?.Id);

            var veiculosAtivos = await _veiculoService.GetVeiculosAsync();
            Veiculos = new ObservableCollection<VeiculoListItem>(veiculosAtivos);
            SelectedVeiculo = _original is not null
                ? Veiculos.FirstOrDefault(v => v.Id == _original.Veiculo?.Id)
                : Veiculos.Count == 1 ? Veiculos[0] : null;

            await Financeiro.LoadAsync("receita", null, _original?.Financeiro);

            if (Rotas.Count == 0)
            {
                ErrorMessage = "Nenhuma rota cadastrada. Cadastre a rota antes de registrar a viagem.";
            }

            _opcoesCarregadas = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível carregar rotas, veículos e categorias.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;

        if (SelectedRota is null)
        {
            ErrorMessage = "Selecione a rota.";
            return;
        }

        if (SelectedVeiculo is null)
        {
            ErrorMessage = "Selecione o veículo.";
            return;
        }

        if (!FormParsing.TryParseKmOpcional(KmSaida, out var kmSaidaInt) || !FormParsing.TryParseKmOpcional(KmChegada, out var kmChegadaInt))
        {
            ErrorMessage = "Informe uma quilometragem válida.";
            return;
        }

        if (kmSaidaInt is int saida && kmChegadaInt is int chegada && chegada < saida)
        {
            ErrorMessage = "O km de chegada não pode ser menor que o km de saída.";
            return;
        }

        DateTime? dataChegadaFinal = Concluida ? DataChegada : null;
        if (dataChegadaFinal < DataSaida)
        {
            ErrorMessage = "A data de chegada não pode ser anterior à data de saída.";
            return;
        }

        decimal? valorFreteDecimal = null;
        if (LancarFrete)
        {
            if (!FormParsing.TryParseValor(ValorFrete, out var valor))
            {
                ErrorMessage = "Informe o valor do frete.";
                return;
            }
            valorFreteDecimal = valor;

            if (Financeiro.Validar() is string erroFinanceiro)
            {
                ErrorMessage = erroFinanceiro;
                return;
            }
        }

        try
        {
            BusyMessage = "Salvando…";
            IsBusy = true;
            ErrorMessage = null;

            var resultado = await _viagemService.SaveAsync(
                _original?.Id,
                SelectedRota.Id,
                SelectedVeiculo.Id,
                DataSaida,
                dataChegadaFinal,
                kmSaidaInt,
                kmChegadaInt,
                string.IsNullOrWhiteSpace(Cliente) ? null : Cliente.Trim(),
                string.IsNullOrWhiteSpace(Observacoes) ? null : Observacoes.Trim(),
                valorFreteDecimal,
                LancarFrete ? Financeiro.ToDados(dataChegadaFinal ?? DataSaida) : null);

            if (!resultado.Sucesso)
            {
                ErrorMessage = resultado.Erro ?? "Não foi possível salvar a viagem.";
                return;
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado ao salvar a viagem.";
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

        var mensagem = _original.Financeiro switch
        {
            null => string.Empty,
            { IsPago: true } => " O frete já recebido continuará no financeiro.",
            _ => " A receita pendente do frete também será excluída.",
        };

        var confirmar = await Shell.Current.DisplayAlertAsync(
            "Excluir viagem",
            $"Tem certeza que deseja excluir esta viagem?{mensagem}",
            "Excluir",
            "Cancelar");

        if (!confirmar) return;

        try
        {
            BusyMessage = "Excluindo…";
            IsBusy = true;
            var ok = await _viagemService.DeleteAsync(_original.Id);
            if (!ok)
            {
                ErrorMessage = "Não foi possível excluir essa viagem.";
                return;
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado ao excluir a viagem.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
