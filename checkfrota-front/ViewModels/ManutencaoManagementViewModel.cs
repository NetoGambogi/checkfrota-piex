using checkfrota_front.Services;
using checkfrota_front.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace checkfrota_front.ViewModels;

public partial class ManutencaoManagementViewModel : AdminSectionViewModelBase
{
    private readonly ManutencaoService _manutencaoService;

    private List<ManutencaoRowViewModel> _allManutencoes = new();

    private bool _isLoadingManutencoes;

    // Filtro alterado durante um carregamento: recarrega quando ele terminar. Só os filtros usam isso;
    // o comando de carregar apenas ignora chamadas repetidas, porque o RefreshView o executa de novo
    // sempre que IsRefreshing (ligado a IsBusy) vira true, o que viraria uma recarga sem fim.
    private bool _recarregarAoTerminar;

    public FiltroPeriodo Periodo { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInitialLoading))]
    private ObservableCollection<ManutencaoRowViewModel> manutencoes = new();

    [ObservableProperty]
    private string searchText = string.Empty;

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
    public bool ShowInitialLoading => IsBusy && Manutencoes.Count == 0;

    public ManutencaoManagementViewModel(ManutencaoService manutencaoService, ApiAuthService apiAuth) : base(apiAuth)
    {
        _manutencaoService = manutencaoService;
        Periodo.Alterado += (_, _) => RecarregarPorFiltro();
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        await LoadAvatarAsync();
        await LoadManutencoesAsync();
    }

    [RelayCommand]
    private async Task LoadManutencoesAsync()
    {
        if (_isLoadingManutencoes) return;

        try
        {
            _isLoadingManutencoes = true;
            IsBusy = true;
            ErrorMessage = null;

            var items = await _manutencaoService.GetManutencoesAsync(Periodo.DataInicio, Periodo.DataFim);
            _allManutencoes = items.Select(m => new ManutencaoRowViewModel(m)).ToList();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível carregar as manutenções.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
            _isLoadingManutencoes = false;
        }

        if (_recarregarAoTerminar)
        {
            _recarregarAoTerminar = false;
            await LoadManutencoesAsync();
        }
    }

    private void RecarregarPorFiltro()
    {
        if (_isLoadingManutencoes)
        {
            _recarregarAoTerminar = true;
            return;
        }

        _ = LoadManutencoesAsync();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        IEnumerable<ManutencaoRowViewModel> filtered = _allManutencoes;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(m => m.TextoBusca.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        Manutencoes = new ObservableCollection<ManutencaoRowViewModel>(filtered);
        HasNoResults = Manutencoes.Count == 0;
    }

    [RelayCommand]
    private async Task AddManutencaoAsync()
    {
        await Shell.Current.GoToAsync(nameof(ManutencaoFormPage));
    }

    [RelayCommand]
    private async Task EditManutencaoAsync(ManutencaoRowViewModel? row)
    {
        if (row is null) return;

        await Shell.Current.GoToAsync(nameof(ManutencaoFormPage), new Dictionary<string, object>
        {
            ["manutencao"] = row.Original,
        });
    }
}
