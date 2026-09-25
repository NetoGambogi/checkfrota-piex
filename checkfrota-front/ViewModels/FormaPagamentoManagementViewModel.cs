using checkfrota_front.Services;
using checkfrota_front.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace checkfrota_front.ViewModels;

public partial class FormaPagamentoManagementViewModel : AdminSectionViewModelBase
{
    private readonly FormaPagamentoService _formaService;

    private List<FormaPagamentoRowViewModel> _allFormas = new();

    // Gesto de pull-to-refresh do RefreshView seta IsRefreshing (ligado a IsBusy) para true
    // ANTES de disparar o Command — usar IsBusy como guarda de reentrância travaria o carregamento
    // pra sempre nesse caminho, então a reentrância é controlada por essa flag interna à parte.
    private bool _isLoadingFormas;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInitialLoading))]
    private ObservableCollection<FormaPagamentoRowViewModel> formas = new();

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
    public bool ShowInitialLoading => IsBusy && Formas.Count == 0;

    public FormaPagamentoManagementViewModel(FormaPagamentoService formaService, ApiAuthService apiAuth) : base(apiAuth)
    {
        _formaService = formaService;
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        await LoadAvatarAsync();
        await LoadFormasAsync();
    }

    [RelayCommand]
    private async Task LoadFormasAsync()
    {
        if (_isLoadingFormas) return;

        try
        {
            _isLoadingFormas = true;
            IsBusy = true;
            ErrorMessage = null;

            var items = await _formaService.GetFormasAsync();
            _allFormas = items.Select(f => new FormaPagamentoRowViewModel(f)).ToList();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível carregar as formas de pagamento.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
            _isLoadingFormas = false;
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        IEnumerable<FormaPagamentoRowViewModel> filtered = _allFormas;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(f => f.Nome.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        Formas = new ObservableCollection<FormaPagamentoRowViewModel>(filtered);
        HasNoResults = Formas.Count == 0;
    }

    [RelayCommand]
    private async Task AddFormaAsync()
    {
        await Shell.Current.GoToAsync(nameof(FormaPagamentoFormPage));
    }

    [RelayCommand]
    private async Task EditFormaAsync(FormaPagamentoRowViewModel? row)
    {
        if (row is null) return;

        await Shell.Current.GoToAsync(nameof(FormaPagamentoFormPage), new Dictionary<string, object>
        {
            ["forma"] = row.Original,
        });
    }
}
