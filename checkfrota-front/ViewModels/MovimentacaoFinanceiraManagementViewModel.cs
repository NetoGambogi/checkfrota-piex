using checkfrota_front.Services;
using checkfrota_front.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace checkfrota_front.ViewModels;

public partial class MovimentacaoFinanceiraManagementViewModel : AdminSectionViewModelBase
{
    private readonly MovimentacaoFinanceiraService _movimentacaoService;

    private List<MovimentacaoFinanceiraRowViewModel> _todas = new();

    // Mesmo motivo do CategoriaFinanceiraManagementViewModel: pull-to-refresh já seta
    // IsBusy/IsRefreshing antes do Command disparar, então a reentrância usa uma flag à parte.
    private bool _isLoadingMovimentacoes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInitialLoading))]
    private ObservableCollection<MovimentacaoFinanceiraRowViewModel> movimentacoes = new();

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTipoTodosSelected))]
    [NotifyPropertyChangedFor(nameof(IsTipoEntradaSelected))]
    [NotifyPropertyChangedFor(nameof(IsTipoSaidaSelected))]
    private string selectedTipo = "todos";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStatusTodosSelected))]
    [NotifyPropertyChangedFor(nameof(IsStatusPendenteSelected))]
    [NotifyPropertyChangedFor(nameof(IsStatusPagoSelected))]
    private string selectedStatus = "todos";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    [NotifyPropertyChangedFor(nameof(ShowInitialLoading))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool hasNoResults;

    [ObservableProperty]
    private string? errorMessage;

    public bool IsTipoTodosSelected => SelectedTipo == "todos";
    public bool IsTipoEntradaSelected => SelectedTipo == "entrada";
    public bool IsTipoSaidaSelected => SelectedTipo == "saida";

    public bool IsStatusTodosSelected => SelectedStatus == "todos";
    public bool IsStatusPendenteSelected => SelectedStatus == "pendente";
    public bool IsStatusPagoSelected => SelectedStatus == "pago";

    public bool ShowEmptyState => HasNoResults && !IsBusy;
    public bool ShowInitialLoading => IsBusy && Movimentacoes.Count == 0;

    public MovimentacaoFinanceiraManagementViewModel(MovimentacaoFinanceiraService movimentacaoService, ApiAuthService apiAuth) : base(apiAuth)
    {
        _movimentacaoService = movimentacaoService;
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        await LoadAvatarAsync();
        await LoadMovimentacoesAsync();
    }

    [RelayCommand]
    private async Task LoadMovimentacoesAsync()
    {
        if (_isLoadingMovimentacoes) return;

        try
        {
            _isLoadingMovimentacoes = true;
            IsBusy = true;
            ErrorMessage = null;

            var tipo = SelectedTipo == "todos" ? null : SelectedTipo;
            var status = SelectedStatus == "todos" ? null : SelectedStatus;

            var items = await _movimentacaoService.GetMovimentacoesAsync(tipo, status);
            _todas = items.Select(m => new MovimentacaoFinanceiraRowViewModel(m)).ToList();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível carregar os lançamentos.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
            _isLoadingMovimentacoes = false;
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedTipoChanged(string value) => _ = LoadMovimentacoesAsync();

    partial void OnSelectedStatusChanged(string value) => _ = LoadMovimentacoesAsync();

    private void ApplyFilter()
    {
        IEnumerable<MovimentacaoFinanceiraRowViewModel> filtered = _todas;

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(m => m.Descricao.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        Movimentacoes = new ObservableCollection<MovimentacaoFinanceiraRowViewModel>(filtered);
        HasNoResults = Movimentacoes.Count == 0;
    }

    [RelayCommand]
    private void SelectTipoTodos() => SelectedTipo = "todos";

    [RelayCommand]
    private void SelectTipoEntrada() => SelectedTipo = "entrada";

    [RelayCommand]
    private void SelectTipoSaida() => SelectedTipo = "saida";

    [RelayCommand]
    private void SelectStatusTodos() => SelectedStatus = "todos";

    [RelayCommand]
    private void SelectStatusPendente() => SelectedStatus = "pendente";

    [RelayCommand]
    private void SelectStatusPago() => SelectedStatus = "pago";

    [RelayCommand]
    private async Task AddMovimentacaoAsync()
    {
        await Shell.Current.GoToAsync(nameof(MovimentacaoFinanceiraFormPage));
    }

    [RelayCommand]
    private async Task EditMovimentacaoAsync(MovimentacaoFinanceiraRowViewModel? row)
    {
        if (row is null) return;

        await Shell.Current.GoToAsync(nameof(MovimentacaoFinanceiraFormPage), new Dictionary<string, object>
        {
            ["movimentacao"] = row.Original,
        });
    }
}
