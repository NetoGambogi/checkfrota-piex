using checkfrota_front.Helpers;
using checkfrota_front.Models;
using checkfrota_front.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;

namespace checkfrota_front.ViewModels;

/// <summary>
/// Registro de abastecimento pelo motorista: só o consumo (litros, valor, km). A despesa é lançada
/// depois pela frota/financeiro, por isso não há campos financeiros nem edição.
/// </summary>
public partial class MotoristaAbastecimentoFormViewModel : AdminSectionViewModelBase
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private readonly MotoristaService _motoristaService;

    private bool _opcoesCarregadas;

    public List<CombustivelOption> Combustiveis => AbastecimentoFormViewModel.CombustivelOptions;

    [ObservableProperty]
    private ObservableCollection<VeiculoListItem> veiculos = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UltimoKmLabel))]
    private VeiculoListItem? selectedVeiculo;

    public string? UltimoKmLabel => SelectedVeiculo is null
        ? null
        : $"Último km registrado: {SelectedVeiculo.KmAtual.ToString("N0", PtBr)} km";

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
    private CombustivelOption selectedCombustivel = AbastecimentoFormViewModel.CombustivelOptions[0];

    [ObservableProperty]
    private bool tanqueCheio = true;

    [ObservableProperty]
    private string? posto;

    // Foto ou PDF da nota, enviado junto com o abastecimento ao salvar.
    private FileResult? _comprovante;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasComprovante))]
    [NotifyPropertyChangedFor(nameof(HasNoComprovante))]
    private string? comprovanteNome;

    public bool HasComprovante => ComprovanteNome is not null;
    public bool HasNoComprovante => !HasComprovante;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private string? errorMessage;

    public MotoristaAbastecimentoFormViewModel(MotoristaService motoristaService, ApiAuthService apiAuth) : base(apiAuth)
    {
        _motoristaService = motoristaService;
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        await LoadAvatarAsync();

        if (_opcoesCarregadas) return;

        try
        {
            Veiculos = new ObservableCollection<VeiculoListItem>(await _motoristaService.GetVeiculosAsync());
            SelectedVeiculo = await VeiculoDaViagemAtualAsync() ?? (Veiculos.Count == 1 ? Veiculos[0] : null);
            _opcoesCarregadas = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível carregar os veículos.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    /// <summary>Quem abastece no meio de uma viagem quase sempre está no veículo dela.</summary>
    private async Task<VeiculoListItem?> VeiculoDaViagemAtualAsync()
    {
        var emAndamento = await _motoristaService.GetViagensAsync("em_andamento", pagina: 1, porPagina: 1);
        var veiculoId = emAndamento.Viagens.FirstOrDefault()?.Veiculo?.Id;
        return Veiculos.FirstOrDefault(v => v.Id == veiculoId);
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

        if (_comprovante is null)
        {
            ErrorMessage = "Anexe a foto ou o PDF do comprovante do abastecimento.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var resultado = await _motoristaService.RegistrarAbastecimentoAsync(
                SelectedVeiculo.Id,
                Data,
                kmInt,
                litrosDecimal,
                valorDecimal,
                SelectedCombustivel.Key,
                TanqueCheio,
                string.IsNullOrWhiteSpace(Posto) ? null : Posto.Trim(),
                _comprovante);

            if (!resultado.Sucesso)
            {
                ErrorMessage = resultado.Erro ?? "Não foi possível registrar o abastecimento.";
                return;
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado ao registrar o abastecimento.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AnexarComprovanteAsync()
    {
        if (IsBusy) return;

        try
        {
            var arquivo = await Anexos.EscolherComprovanteAsync();
            if (arquivo is null) return;

            var erro = await Anexos.ValidarAsync(arquivo);
            if (erro is not null)
            {
                ErrorMessage = erro;
                return;
            }

            ErrorMessage = null;
            _comprovante = arquivo;
            ComprovanteNome = arquivo.FileName;
        }
        catch (PermissionException)
        {
            ErrorMessage = "Permita o acesso à câmera para fotografar o comprovante.";
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível selecionar o comprovante.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    [RelayCommand]
    private void RemoverComprovante()
    {
        if (IsBusy) return;

        _comprovante = null;
        ComprovanteNome = null;
    }
}
