using checkfrota_front.Services;
using checkfrota_front.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace checkfrota_front.ViewModels;

public partial class RotaManagementViewModel : AdminSectionViewModelBase
{
    private readonly RotaService _rotaService;

    private List<RotaRowViewModel> _allRotas = new();

    private bool _isLoadingRotas;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInitialLoading))]
    private ObservableCollection<RotaRowViewModel> rotas = new();

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
    public bool ShowInitialLoading => IsBusy && Rotas.Count == 0;

    public RotaManagementViewModel(RotaService rotaService, ApiAuthService apiAuth) : base(apiAuth)
    {
        _rotaService = rotaService;
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        await LoadAvatarAsync();
        await LoadRotasAsync();
    }

    [RelayCommand]
    private async Task LoadRotasAsync()
    {
        if (_isLoadingRotas) return;

        try
        {
            _isLoadingRotas = true;
            IsBusy = true;
            ErrorMessage = null;

            var items = await _rotaService.GetRotasAsync();
            _allRotas = items.Select(r => new RotaRowViewModel(r)).ToList();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível carregar as rotas.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
            _isLoadingRotas = false;
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        IEnumerable<RotaRowViewModel> filtered = _allRotas;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(r =>
                r.Nome.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || r.Trajeto.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        Rotas = new ObservableCollection<RotaRowViewModel>(filtered);
        HasNoResults = Rotas.Count == 0;
    }

    [RelayCommand]
    private async Task AddRotaAsync()
    {
        await Shell.Current.GoToAsync(nameof(RotaFormPage));
    }

    [RelayCommand]
    private async Task EditRotaAsync(RotaRowViewModel? row)
    {
        if (row is null) return;

        await Shell.Current.GoToAsync(nameof(RotaFormPage), new Dictionary<string, object>
        {
            ["rota"] = row.Original,
        });
    }
}
