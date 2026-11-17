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

    // Filtro trocado durante uma carga: recarrega ao terminar, para a lista seguir o filtro atual.
    private bool _recarregarAoTerminar;

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStatusTodosSelected))]
    [NotifyPropertyChangedFor(nameof(IsStatusPendenteSelected))]
    [NotifyPropertyChangedFor(nameof(IsStatusConcluidoSelected))]
    [NotifyPropertyChangedFor(nameof(FiltrosResumoLabel))]
    [NotifyPropertyChangedFor(nameof(EmptyStateLabel))]
    private string selectedStatus = "todos";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FiltrosChevronRotation))]
    [NotifyPropertyChangedFor(nameof(IsFiltrosColapsado))]
    private bool isFiltrosExpandido;

    public bool IsStatusTodosSelected => SelectedStatus == "todos";
    public bool IsStatusPendenteSelected => SelectedStatus == "pendente";
    public bool IsStatusConcluidoSelected => SelectedStatus == "concluido";

    public double FiltrosChevronRotation => IsFiltrosExpandido ? 90 : 0;
    public bool IsFiltrosColapsado => !IsFiltrosExpandido;

    public string FiltrosResumoLabel => SelectedStatus switch
    {
        "pendente" => "Somente pendentes",
        "concluido" => "Somente concluídos",
        _ => "Todos os financiamentos",
    };

    public string EmptyStateLabel => SelectedStatus switch
    {
        "pendente" => "Nenhum financiamento pendente.",
        "concluido" => "Nenhum financiamento concluído.",
        _ => "Nenhum financiamento cadastrado.",
    };

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
        if (_isLoadingFinanciamentos)
        {
            _recarregarAoTerminar = true;
            return;
        }

        try
        {
            _isLoadingFinanciamentos = true;
            IsBusy = true;
            ErrorMessage = null;

            var items = await _financiamentoService.GetFinanciamentosAsync(SelectedStatus);
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

        if (_recarregarAoTerminar)
        {
            _recarregarAoTerminar = false;
            await LoadFinanciamentosAsync();
        }
    }

    partial void OnSelectedStatusChanged(string value) => _ = LoadFinanciamentosAsync();

    [RelayCommand]
    private void SelectStatusTodos() => SelectedStatus = "todos";

    [RelayCommand]
    private void SelectStatusPendente() => SelectedStatus = "pendente";

    [RelayCommand]
    private void SelectStatusConcluido() => SelectedStatus = "concluido";

    [RelayCommand]
    private void ToggleFiltros() => IsFiltrosExpandido = !IsFiltrosExpandido;

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
