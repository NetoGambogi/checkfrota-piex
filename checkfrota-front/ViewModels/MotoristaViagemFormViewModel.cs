using checkfrota_front.Helpers;
using checkfrota_front.Models;
using checkfrota_front.Services;
using checkfrota_front.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;

namespace checkfrota_front.ViewModels;

/// <summary>
/// Iniciar ou editar uma viagem em andamento do motorista. Sem frete nem chegada: o frete é da
/// frota/financeiro e a chegada é registrada em <see cref="ConcluirViagemPage"/>.
/// </summary>
public partial class MotoristaViagemFormViewModel : AdminSectionViewModelBase, IQueryAttributable
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private readonly MotoristaService _motoristaService;

    private ViagemListItem? _original;
    private bool _opcoesCarregadas;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageTitle))]
    [NotifyPropertyChangedFor(nameof(IsCreating))]
    [NotifyPropertyChangedFor(nameof(SaveButtonText))]
    private bool isEditing;

    public bool IsCreating => !IsEditing;

    public string PageTitle => IsEditing ? "Editar viagem" : "Iniciar viagem";

    public string SaveButtonText => IsEditing ? "Salvar alterações" : "Iniciar viagem";

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
    [NotifyPropertyChangedFor(nameof(KmAtualLabel))]
    [NotifyPropertyChangedFor(nameof(VeiculoEmUsoLabel))]
    [NotifyPropertyChangedFor(nameof(HasVeiculoEmUso))]
    private VeiculoListItem? selectedVeiculo;

    /// <summary>
    /// Aviso quando o veículo escolhido já está numa viagem em andamento (a API recusa). Na edição,
    /// a viagem em andamento do próprio veículo é esta mesma, então não conta.
    /// </summary>
    public string? VeiculoEmUsoLabel => SelectedVeiculo switch
    {
        null or { EmViagem: false } => null,
        var v when IsEditing && v.Id == _original?.Veiculo?.Id => null,
        { EmViagemPropria: true } => "Você já está em uma viagem com este veículo. Conclua-a antes de iniciar outra.",
        var v => $"Em viagem com {v.EmViagemCom}. Escolha outro veículo.",
    };

    public bool HasVeiculoEmUso => VeiculoEmUsoLabel is not null;

    public string? KmAtualLabel => SelectedVeiculo is null
        ? null
        : $"Último km registrado do veículo: {SelectedVeiculo.KmAtual.ToString("N0", PtBr)} km";

    [ObservableProperty]
    private DateTime dataSaida = DateTime.Today;

    [ObservableProperty]
    private string kmSaida = string.Empty;

    [ObservableProperty]
    private string? cliente;

    [ObservableProperty]
    private string? observacoes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private string? errorMessage;

    public MotoristaViagemFormViewModel(MotoristaService motoristaService, ApiAuthService apiAuth) : base(apiAuth)
    {
        _motoristaService = motoristaService;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("viagem", out var viagemObj) && viagemObj is ViagemListItem item)
        {
            _original = item;
            if (DateTime.TryParse(item.DataSaida, out var saida)) DataSaida = saida;
            KmSaida = item.KmSaida?.ToString() ?? string.Empty;
            Cliente = item.Cliente;
            Observacoes = item.Observacoes;
            IsEditing = true;
        }
    }

    /// <summary>Ao trocar o veículo, sugere o km atual dele como km de saída.</summary>
    partial void OnSelectedVeiculoChanged(VeiculoListItem? oldValue, VeiculoListItem? newValue)
    {
        if (newValue is null) return;

        var kmSugeridoAnterior = oldValue?.KmAtual.ToString();
        var campoIntocado = string.IsNullOrWhiteSpace(KmSaida) || KmSaida == kmSugeridoAnterior;

        if (IsCreating && campoIntocado)
        {
            KmSaida = newValue.KmAtual.ToString();
        }
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        await LoadAvatarAsync();

        if (_opcoesCarregadas) return;

        try
        {
            Rotas = new ObservableCollection<RotaListItem>(await _motoristaService.GetRotasAsync());
            SelectedRota = Rotas.FirstOrDefault(r => r.Id == _original?.Rota?.Id);

            Veiculos = new ObservableCollection<VeiculoListItem>(await _motoristaService.GetVeiculosAsync());
            SelectedVeiculo = _original is not null
                ? Veiculos.FirstOrDefault(v => v.Id == _original.Veiculo?.Id)
                : Veiculos.Count == 1 ? Veiculos[0] : null;

            if (Rotas.Count == 0)
            {
                ErrorMessage = "Nenhuma rota cadastrada. Peça à gestão da frota para cadastrar a rota.";
            }

            _opcoesCarregadas = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível carregar rotas e veículos.";
            System.Diagnostics.Debug.WriteLine(ex);
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

        if (VeiculoEmUsoLabel is string emUso)
        {
            ErrorMessage = emUso;
            return;
        }

        if (!FormParsing.TryParseKmOpcional(KmSaida, out var kmSaidaInt))
        {
            ErrorMessage = "Informe uma quilometragem válida.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var resultado = await _motoristaService.SaveViagemAsync(
                _original?.Id,
                SelectedRota.Id,
                SelectedVeiculo.Id,
                DataSaida,
                kmSaidaInt,
                string.IsNullOrWhiteSpace(Cliente) ? null : Cliente.Trim(),
                string.IsNullOrWhiteSpace(Observacoes) ? null : Observacoes.Trim());

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

    /// <summary>Troca este formulário pela tela de conclusão da mesma viagem.</summary>
    [RelayCommand]
    private async Task ConcluirAsync()
    {
        if (IsBusy || _original is null) return;

        await Shell.Current.GoToAsync($"../{nameof(ConcluirViagemPage)}", new Dictionary<string, object>
        {
            ["viagem"] = _original,
        });
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (IsBusy || _original is null) return;

        var confirmar = await Shell.Current.DisplayAlertAsync(
            "Excluir viagem",
            "Excluir esta viagem? Use só se ela foi iniciada por engano.",
            "Excluir",
            "Cancelar");

        if (!confirmar) return;

        try
        {
            IsBusy = true;
            var ok = await _motoristaService.DeleteViagemAsync(_original.Id);
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
