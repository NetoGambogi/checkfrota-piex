using checkfrota_front.Services;
using checkfrota_front.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace checkfrota_front.ViewModels;

public partial class FinanciamentoManagementViewModel : AdminSectionViewModelBase
{
    private readonly FinanciamentoService _financiamentoService;

    private bool _isLoadingFinanciamentos;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInitialLoading))]
    private ObservableCollection<FinanciamentoRowViewModel> financiamentos = new();

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
    public bool ShowInitialLoading => IsBusy && Financiamentos.Count == 0;

    public FinanciamentoManagementViewModel(FinanciamentoService financiamentoService, ApiAuthService apiAuth) : base(apiAuth)
    {
        _financiamentoService = financiamentoService;
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        await LoadAvatarAsync();
        await LoadFinanciamentosAsync();
    }

    [RelayCommand]
    private async Task LoadFinanciamentosAsync()
    {
        if (_isLoadingFinanciamentos) return;

        try
        {
            _isLoadingFinanciamentos = true;
            IsBusy = true;
            ErrorMessage = null;

            var items = await _financiamentoService.GetFinanciamentosAsync();
            Financiamentos = new ObservableCollection<FinanciamentoRowViewModel>(items.Select(f => new FinanciamentoRowViewModel(f)));
            HasNoResults = Financiamentos.Count == 0;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível carregar os financiamentos.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
            _isLoadingFinanciamentos = false;
        }
    }

    [RelayCommand]
    private async Task AddFinanciamentoAsync()
    {
        await Shell.Current.GoToAsync(nameof(FinanciamentoFormPage));
    }

    [RelayCommand]
    private async Task OpenFinanciamentoAsync(FinanciamentoRowViewModel? row)
    {
        if (row is null) return;

        await Shell.Current.GoToAsync(nameof(FinanciamentoDetalhePage), new Dictionary<string, object>
        {
            ["financiamentoId"] = row.Id,
        });
    }
}
