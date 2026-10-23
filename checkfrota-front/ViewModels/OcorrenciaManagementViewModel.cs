using checkfrota_front.Services;
using checkfrota_front.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace checkfrota_front.ViewModels;

/// <summary>
/// Ocorrências relatadas pelos motoristas, para a gestão da frota acompanhar e resolver. As abertas
/// vêm das mais graves para as mais leves.
/// </summary>
public partial class OcorrenciaManagementViewModel : AdminSectionViewModelBase
{
    private const string Abertas = "aberta";
    private const string Resolvidas = "resolvida";

    private readonly OcorrenciaService _ocorrenciaService;

    // Evita a recarga sem fim do RefreshView e respostas da aba anterior (ver MinhasViagensViewModel).
    private bool _isLoadingOcorrencias;
    private bool _recarregarAoTerminar;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAbaAbertas))]
    [NotifyPropertyChangedFor(nameof(IsAbaResolvidas))]
    [NotifyPropertyChangedFor(nameof(EmptyMessage))]
    private string aba = Abertas;

    public bool IsAbaAbertas => Aba == Abertas;
    public bool IsAbaResolvidas => Aba == Resolvidas;

    public string EmptyMessage => IsAbaAbertas
        ? "Nenhuma ocorrência aberta. Tudo certo na estrada."
        : "Nenhuma ocorrência resolvida ainda.";

    [ObservableProperty]
    private string abertasLabel = "Abertas";

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

    public OcorrenciaManagementViewModel(OcorrenciaService ocorrenciaService, ApiAuthService apiAuth) : base(apiAuth)
    {
        _ocorrenciaService = ocorrenciaService;
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        await LoadAvatarAsync();
        await LoadOcorrenciasAsync();
    }

    [RelayCommand]
    private async Task SelecionarAbaAsync(string novaAba)
    {
        if (novaAba == Aba) return;

        Aba = novaAba;
        Ocorrencias = new ObservableCollection<OcorrenciaRowViewModel>();

        if (_isLoadingOcorrencias)
        {
            _recarregarAoTerminar = true;
            return;
        }

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

            var abaCarregada = Aba;
            var resposta = await _ocorrenciaService.GetOcorrenciasAsync(abaCarregada);
            AbertasLabel = resposta.Abertas > 0 ? $"Abertas ({resposta.Abertas})" : "Abertas";
            if (abaCarregada != Aba) return;

            Ocorrencias = new ObservableCollection<OcorrenciaRowViewModel>(resposta.Ocorrencias.Select(o => new OcorrenciaRowViewModel(o)));
            HasNoResults = Ocorrencias.Count == 0;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível carregar as ocorrências.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
            _isLoadingOcorrencias = false;
        }

        if (_recarregarAoTerminar)
        {
            _recarregarAoTerminar = false;
            await LoadOcorrenciasAsync();
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
