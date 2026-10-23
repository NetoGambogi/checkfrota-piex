using checkfrota_front.Services;
using checkfrota_front.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace checkfrota_front.ViewModels;

/// <summary>Ocorrências relatadas pelo motorista, para ele acompanhar se a frota já resolveu.</summary>
public partial class MinhasOcorrenciasViewModel : AdminSectionViewModelBase
{
    private readonly MotoristaService _motoristaService;

    // Evita a recarga sem fim do RefreshView (ver ViagemManagementViewModel).
    private bool _isLoadingOcorrencias;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInitialLoading))]
    private ObservableCollection<OcorrenciaRowViewModel> ocorrencias = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    [NotifyPropertyChangedFor(nameof(ShowInitialLoading))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool hasNoResults;

    [ObservableProperty]
    private string? errorMessage;

    public bool ShowEmptyState => HasNoResults && !IsBusy;
    public bool ShowInitialLoading => IsBusy && Ocorrencias.Count == 0;

    public MinhasOcorrenciasViewModel(MotoristaService motoristaService, ApiAuthService apiAuth) : base(apiAuth)
    {
        _motoristaService = motoristaService;
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        await LoadAvatarAsync();
        await LoadOcorrenciasAsync();
    }

    [RelayCommand]
    private async Task LoadOcorrenciasAsync()
    {
        if (_isLoadingOcorrencias) return;

        try
        {
            _isLoadingOcorrencias = true;
            IsBusy = true;
            ErrorMessage = null;

            var items = await _motoristaService.GetOcorrenciasAsync();
            Ocorrencias = new ObservableCollection<OcorrenciaRowViewModel>(items.Select(o => new OcorrenciaRowViewModel(o)));
            HasNoResults = Ocorrencias.Count == 0;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível carregar suas ocorrências.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
            _isLoadingOcorrencias = false;
        }
    }

    [RelayCommand]
    private async Task AbrirOcorrenciaAsync(OcorrenciaRowViewModel? row)
    {
        if (row is null) return;

        await Shell.Current.GoToAsync(nameof(OcorrenciaDetalhePage), new Dictionary<string, object>
        {
            ["ocorrencia"] = row.Original,
        });
    }
}
