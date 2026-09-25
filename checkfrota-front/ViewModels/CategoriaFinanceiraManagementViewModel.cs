using checkfrota_front.Services;
using checkfrota_front.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace checkfrota_front.ViewModels;

public partial class CategoriaFinanceiraManagementViewModel : AdminSectionViewModelBase
{
    private readonly CategoriaFinanceiraService _categoriaService;

    private List<CategoriaFinanceiraRowViewModel> _allCategorias = new();

    // Gesto de pull-to-refresh do RefreshView seta IsRefreshing (ligado a IsBusy) para true
    // ANTES de disparar o Command — usar IsBusy como guarda de reentrância travaria o carregamento
    // pra sempre nesse caminho, então a reentrância é controlada por essa flag interna à parte.
    private bool _isLoadingCategorias;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInitialLoading))]
    private ObservableCollection<CategoriaFinanceiraRowViewModel> categorias = new();

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDespesaSelected))]
    [NotifyPropertyChangedFor(nameof(IsReceitaSelected))]
    private string selectedTipo = "despesa";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    [NotifyPropertyChangedFor(nameof(ShowInitialLoading))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool hasNoResults;

    [ObservableProperty]
    private string? errorMessage;

    public bool IsDespesaSelected => SelectedTipo == "despesa";
    public bool IsReceitaSelected => SelectedTipo == "receita";

    public bool ShowEmptyState => HasNoResults && !IsBusy;
    public bool ShowInitialLoading => IsBusy && Categorias.Count == 0;

    public CategoriaFinanceiraManagementViewModel(CategoriaFinanceiraService categoriaService, ApiAuthService apiAuth) : base(apiAuth)
    {
        _categoriaService = categoriaService;
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        await LoadAvatarAsync();
        await LoadCategoriasAsync();
    }

    [RelayCommand]
    private async Task LoadCategoriasAsync()
    {
        if (_isLoadingCategorias) return;

        try
        {
            _isLoadingCategorias = true;
            IsBusy = true;
            ErrorMessage = null;

            var items = await _categoriaService.GetCategoriasAsync(SelectedTipo);
            _allCategorias = items.Select(c => new CategoriaFinanceiraRowViewModel(c)).ToList();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível carregar as categorias.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
            _isLoadingCategorias = false;
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedTipoChanged(string value) => _ = LoadCategoriasAsync();

    private void ApplyFilter()
    {
        IEnumerable<CategoriaFinanceiraRowViewModel> filtered = _allCategorias;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(c => c.Nome.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        Categorias = new ObservableCollection<CategoriaFinanceiraRowViewModel>(filtered);
        HasNoResults = Categorias.Count == 0;
    }

    [RelayCommand]
    private void SelectDespesa() => SelectedTipo = "despesa";

    [RelayCommand]
    private void SelectReceita() => SelectedTipo = "receita";

    [RelayCommand]
    private async Task AddCategoriaAsync()
    {
        await Shell.Current.GoToAsync(nameof(CategoriaFinanceiraFormPage), new Dictionary<string, object>
        {
            ["tipo"] = SelectedTipo,
        });
    }

    [RelayCommand]
    private async Task EditCategoriaAsync(CategoriaFinanceiraRowViewModel? row)
    {
        if (row is null) return;

        await Shell.Current.GoToAsync(nameof(CategoriaFinanceiraFormPage), new Dictionary<string, object>
        {
            ["categoria"] = row.Original,
        });
    }
}
