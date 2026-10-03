using checkfrota_front.Services;
using checkfrota_front.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace checkfrota_front.ViewModels;

public partial class VeiculoManagementViewModel : AdminSectionViewModelBase
{
    private readonly VeiculoService _veiculoService;

    private List<VeiculoRowViewModel> _allVeiculos = new();

    // Mesmo motivo de CategoriaFinanceiraManagementViewModel: o pull-to-refresh liga IsBusy antes
    // de disparar o Command, então a reentrância é controlada por uma flag à parte.
    private bool _isLoadingVeiculos;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInitialLoading))]
    private ObservableCollection<VeiculoRowViewModel> veiculos = new();

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
    public bool ShowInitialLoading => IsBusy && Veiculos.Count == 0;

    public VeiculoManagementViewModel(VeiculoService veiculoService, ApiAuthService apiAuth) : base(apiAuth)
    {
        _veiculoService = veiculoService;
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        await LoadAvatarAsync();
        await LoadVeiculosAsync();
    }

    [RelayCommand]
    private async Task LoadVeiculosAsync()
    {
        if (_isLoadingVeiculos) return;

        try
        {
            _isLoadingVeiculos = true;
            IsBusy = true;
            ErrorMessage = null;

            var items = await _veiculoService.GetVeiculosAsync();
            _allVeiculos = items.Select(v => new VeiculoRowViewModel(v)).ToList();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível carregar os veículos.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
            _isLoadingVeiculos = false;
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        IEnumerable<VeiculoRowViewModel> filtered = _allVeiculos;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var termoPlaca = SearchText.Replace("-", "").Replace(" ", "");
            filtered = filtered.Where(v =>
                v.Placa.Contains(termoPlaca, StringComparison.OrdinalIgnoreCase)
                || v.Descricao.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        Veiculos = new ObservableCollection<VeiculoRowViewModel>(filtered);
        HasNoResults = Veiculos.Count == 0;
    }

    [RelayCommand]
    private async Task AddVeiculoAsync()
    {
        await Shell.Current.GoToAsync(nameof(VeiculoFormPage));
    }

    [RelayCommand]
    private async Task EditVeiculoAsync(VeiculoRowViewModel? row)
    {
        if (row is null) return;

        await Shell.Current.GoToAsync(nameof(VeiculoFormPage), new Dictionary<string, object>
        {
            ["veiculo"] = row.Original,
        });
    }
}
