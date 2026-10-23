using checkfrota_front.Services;
using checkfrota_front.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace checkfrota_front.ViewModels;

public partial class FrotaHomeViewModel : AdminSectionViewModelBase
{
    private readonly OcorrenciaService _ocorrenciaService;

    /// <summary>Ocorrências abertas relatadas pelos motoristas, em destaque no card.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOcorrenciasAbertas))]
    [NotifyPropertyChangedFor(nameof(OcorrenciasAbertasTexto))]
    private int ocorrenciasAbertas;

    public bool HasOcorrenciasAbertas => OcorrenciasAbertas > 0;
    public string OcorrenciasAbertasTexto => OcorrenciasAbertas > 99 ? "99+" : OcorrenciasAbertas.ToString();

    public FrotaHomeViewModel(OcorrenciaService ocorrenciaService, ApiAuthService apiAuth) : base(apiAuth)
    {
        _ocorrenciaService = ocorrenciaService;
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        await LoadAvatarAsync();

        try
        {
            OcorrenciasAbertas = (await _ocorrenciaService.GetOcorrenciasAsync()).Abertas;
        }
        catch (Exception ex)
        {
            // O contador é só um destaque: sem ele, a tela continua utilizável.
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    [RelayCommand]
    private async Task AbrirVeiculosAsync()
    {
        await Shell.Current.GoToAsync(nameof(VeiculoManagementPage));
    }

    [RelayCommand]
    private async Task AbrirManutencoesAsync()
    {
        await Shell.Current.GoToAsync(nameof(ManutencaoManagementPage));
    }

    [RelayCommand]
    private async Task AbrirRotasAsync()
    {
        await Shell.Current.GoToAsync(nameof(RotaManagementPage));
    }

    [RelayCommand]
    private async Task AbrirViagensAsync()
    {
        await Shell.Current.GoToAsync(nameof(ViagemManagementPage));
    }

    [RelayCommand]
    private async Task AbrirAbastecimentosAsync()
    {
        await Shell.Current.GoToAsync(nameof(AbastecimentoManagementPage));
    }

    [RelayCommand]
    private async Task AbrirOcorrenciasAsync()
    {
        await Shell.Current.GoToAsync(nameof(OcorrenciaManagementPage));
    }
}
