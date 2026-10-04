using checkfrota_front.Services;
using checkfrota_front.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace checkfrota_front.ViewModels;

public partial class ViagemManagementViewModel : AdminSectionViewModelBase
{
    private readonly ViagemService _viagemService;

    private List<ViagemRowViewModel> _allViagens = new();

    private bool _isLoadingViagens;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInitialLoading))]
    private ObservableCollection<ViagemRowViewModel> viagens = new();

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
    public bool ShowInitialLoading => IsBusy && Viagens.Count == 0;

    public ViagemManagementViewModel(ViagemService viagemService, ApiAuthService apiAuth) : base(apiAuth)
    {
        _viagemService = viagemService;
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        await LoadAvatarAsync();
        await LoadViagensAsync();
    }

    [RelayCommand]
    private async Task LoadViagensAsync()
    {
        if (_isLoadingViagens) return;

        try
        {
            _isLoadingViagens = true;
            IsBusy = true;
            ErrorMessage = null;

            var items = await _viagemService.GetViagensAsync();
            _allViagens = items.Select(v => new ViagemRowViewModel(v)).ToList();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível carregar as viagens.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
            _isLoadingViagens = false;
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        IEnumerable<ViagemRowViewModel> filtered = _allViagens;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(v => v.TextoBusca.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        Viagens = new ObservableCollection<ViagemRowViewModel>(filtered);
        HasNoResults = Viagens.Count == 0;
    }

    [RelayCommand]
    private async Task AddViagemAsync()
    {
        await Shell.Current.GoToAsync(nameof(ViagemFormPage));
    }

    [RelayCommand]
    private async Task EditViagemAsync(ViagemRowViewModel? row)
    {
        if (row is null) return;

        await Shell.Current.GoToAsync(nameof(ViagemFormPage), new Dictionary<string, object>
        {
            ["viagem"] = row.Original,
        });
    }
}
