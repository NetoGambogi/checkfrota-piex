using checkfrota_front.Services;
using checkfrota_front.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace checkfrota_front.ViewModels;

public partial class MotoristaHomeViewModel : AdminSectionViewModelBase
{
    private readonly MotoristaService _motoristaService;

    [ObservableProperty]
    private string? saudacao;

    [ObservableProperty]
    private bool isOnline = true;

    public string StatusTexto => IsOnline ? "ON-LINE" : "OFF-LINE";

    /// <summary>Viagem em andamento mais recente; o card de destaque mostra ela.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasViagemAtual))]
    [NotifyPropertyChangedFor(nameof(SemViagemAtual))]
    private ViagemRowViewModel? viagemAtual;

    public bool HasViagemAtual => ViagemAtual is not null;
    public bool SemViagemAtual => ViagemAtual is null && !IsLoading;

    [ObservableProperty]
    private string? outrasViagensLabel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SemViagemAtual))]
    private bool isLoading;

    [ObservableProperty]
    private string? errorMessage;

    public MotoristaHomeViewModel(MotoristaService motoristaService, ApiAuthService apiAuth) : base(apiAuth)
    {
        _motoristaService = motoristaService;
    }

    partial void OnIsOnlineChanged(bool value) => OnPropertyChanged(nameof(StatusTexto));

    [RelayCommand]
    private async Task AppearingAsync()
    {
        var nome = await SecureStorage.GetAsync("user_name");
        var primeiroNome = nome?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        var periodo = DateTime.Now.Hour switch
        {
            < 12 => "Bom dia",
            < 18 => "Boa tarde",
            _ => "Boa noite",
        };
        Saudacao = primeiroNome is null ? $"{periodo}!" : $"{periodo}, {primeiroNome}!";
        IsOnline = Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

        await LoadAvatarAsync();
        await LoadViagemAtualAsync();
    }

    private async Task LoadViagemAtualAsync()
    {
        if (IsLoading) return;

        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var resposta = await _motoristaService.GetViagensAsync("em_andamento", pagina: 1, porPagina: 1);
            ViagemAtual = resposta.Viagens.FirstOrDefault() is { } viagem ? new ViagemRowViewModel(viagem) : null;

            var outras = resposta.Paginacao.Total - 1;
            OutrasViagensLabel = outras switch
            {
                <= 0 => null,
                1 => "Você tem mais 1 viagem em andamento.",
                _ => $"Você tem mais {outras} viagens em andamento.",
            };
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível carregar sua viagem atual.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private Task IniciarViagemAsync() => Shell.Current.GoToAsync(nameof(MotoristaViagemFormPage));

    [RelayCommand]
    private async Task ConcluirViagemAtualAsync()
    {
        if (ViagemAtual is null) return;

        await Shell.Current.GoToAsync(nameof(ConcluirViagemPage), new Dictionary<string, object>
        {
            ["viagem"] = ViagemAtual.Original,
        });
    }

    [RelayCommand]
    private async Task EditarViagemAtualAsync()
    {
        if (ViagemAtual is null) return;

        await Shell.Current.GoToAsync(nameof(MotoristaViagemFormPage), new Dictionary<string, object>
        {
            ["viagem"] = ViagemAtual.Original,
        });
    }

    [RelayCommand]
    private async Task RegistrarOcorrenciaAsync()
    {
        if (ViagemAtual is null) return;

        await Shell.Current.GoToAsync(nameof(MotoristaOcorrenciaFormPage), new Dictionary<string, object>
        {
            ["viagem"] = ViagemAtual.Original,
        });
    }

    [RelayCommand]
    private Task AbrirOcorrenciasAsync() => Shell.Current.GoToAsync(nameof(MinhasOcorrenciasPage));

    [RelayCommand]
    private Task RegistrarAbastecimentoAsync() => Shell.Current.GoToAsync(nameof(MotoristaAbastecimentoFormPage));

    [RelayCommand]
    private Task AbrirViagensAsync() => Shell.Current.GoToAsync("//motorista/viagens");

    [RelayCommand]
    private Task AbrirAbastecimentosAsync() => Shell.Current.GoToAsync(nameof(MeusAbastecimentosPage));
}
