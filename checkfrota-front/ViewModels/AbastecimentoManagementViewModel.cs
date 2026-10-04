using checkfrota_front.Models;
using checkfrota_front.Services;
using checkfrota_front.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;

namespace checkfrota_front.ViewModels;

public class FiltroVeiculoOption
{
    public int? Id { get; set; }
    public string Label { get; set; } = string.Empty;
}

/// <summary>Média de consumo de um veículo na lista "Média por veículo".</summary>
public class ConsumoVeiculoRowViewModel
{
    public string Placa { get; }
    public string MediaLabel { get; }
    public string CustoLabel { get; }

    public ConsumoVeiculoRowViewModel(ConsumoVeiculo consumo, CultureInfo cultura)
    {
        Placa = consumo.Veiculo.Placa;
        MediaLabel = consumo.MediaKmL is decimal media ? $"{media.ToString("N2", cultura)} km/l" : "sem média";
        CustoLabel = consumo.CustoPorKm is decimal custo ? $"{custo.ToString("C2", cultura)}/km" : string.Empty;
    }
}

public partial class AbastecimentoManagementViewModel : AdminSectionViewModelBase
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly FiltroVeiculoOption TodosOsVeiculos = new() { Id = null, Label = "Todos os veículos" };

    private readonly AbastecimentoService _abastecimentoService;
    private readonly VeiculoService _veiculoService;

    private bool _isLoadingAbastecimentos;

    // Filtro alterado durante um carregamento: recarrega quando ele terminar. Só os filtros usam isso;
    // o comando de carregar apenas ignora chamadas repetidas, porque o RefreshView o executa de novo
    // sempre que IsRefreshing (ligado a IsBusy) vira true, o que viraria uma recarga sem fim.
    private bool _recarregarAoTerminar;

    public FiltroPeriodo Periodo { get; } = new();
    private bool _filtrosCarregados;

    [ObservableProperty]
    private List<FiltroVeiculoOption> filtrosVeiculo = new() { TodosOsVeiculos };

    [ObservableProperty]
    private FiltroVeiculoOption selectedFiltroVeiculo = TodosOsVeiculos;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInitialLoading))]
    private ObservableCollection<AbastecimentoRowViewModel> abastecimentos = new();

    [ObservableProperty]
    private string mediaLabel = "—";

    [ObservableProperty]
    private string precoMedioLitroLabel = "—";

    [ObservableProperty]
    private string custoPorKmLabel = "—";

    [ObservableProperty]
    private string custoMedioAbastecimentoLabel = "—";

    [ObservableProperty]
    private string totaisLabel = string.Empty;

    [ObservableProperty]
    private string? semMediaLabel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowMediasPorVeiculo))]
    private ObservableCollection<ConsumoVeiculoRowViewModel> mediasPorVeiculo = new();

    public bool ShowMediasPorVeiculo => MediasPorVeiculo.Count > 1;

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
    public bool ShowInitialLoading => IsBusy && Abastecimentos.Count == 0;

    public AbastecimentoManagementViewModel(AbastecimentoService abastecimentoService, VeiculoService veiculoService, ApiAuthService apiAuth) : base(apiAuth)
    {
        _abastecimentoService = abastecimentoService;
        _veiculoService = veiculoService;
        Periodo.Alterado += (_, _) => RecarregarPorFiltro();
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        await LoadAvatarAsync();
        await CarregarFiltrosAsync();
        await LoadAbastecimentosAsync();
    }

    private async Task CarregarFiltrosAsync()
    {
        if (_filtrosCarregados) return;

        try
        {
            var veiculos = await _veiculoService.GetVeiculosAsync();
            var opcoes = new List<FiltroVeiculoOption> { TodosOsVeiculos };
            opcoes.AddRange(veiculos.Select(v => new FiltroVeiculoOption { Id = v.Id, Label = v.Rotulo }));
            FiltrosVeiculo = opcoes;
            _filtrosCarregados = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    partial void OnSelectedFiltroVeiculoChanged(FiltroVeiculoOption value)
    {
        if (_filtrosCarregados) RecarregarPorFiltro();
    }

    [RelayCommand]
    private async Task LoadAbastecimentosAsync()
    {
        if (_isLoadingAbastecimentos) return;

        try
        {
            _isLoadingAbastecimentos = true;
            IsBusy = true;
            ErrorMessage = null;

            var veiculoId = SelectedFiltroVeiculo?.Id;
            var itemsTask = _abastecimentoService.GetAbastecimentosAsync(Periodo.DataInicio, Periodo.DataFim, veiculoId);
            var consumoTask = _abastecimentoService.GetConsumoAsync(Periodo.DataInicio, Periodo.DataFim, veiculoId);
            await Task.WhenAll(itemsTask, consumoTask);

            Abastecimentos = new ObservableCollection<AbastecimentoRowViewModel>(itemsTask.Result.Select(a => new AbastecimentoRowViewModel(a)));
            HasNoResults = Abastecimentos.Count == 0;
            AplicarResumo(consumoTask.Result);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível carregar os abastecimentos.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
            _isLoadingAbastecimentos = false;
        }

        if (_recarregarAoTerminar)
        {
            _recarregarAoTerminar = false;
            await LoadAbastecimentosAsync();
        }
    }

    private void RecarregarPorFiltro()
    {
        if (_isLoadingAbastecimentos)
        {
            _recarregarAoTerminar = true;
            return;
        }

        _ = LoadAbastecimentosAsync();
    }

    private void AplicarResumo(ConsumoResponse? consumo)
    {
        // Com um veículo filtrado, a API devolve só ele; o "geral" então é o próprio veículo.
        var resumo = consumo?.Geral ?? new ConsumoResumo();

        MediaLabel = resumo.MediaKmL is decimal media ? $"{media.ToString("N2", PtBr)} km/l" : "—";
        PrecoMedioLitroLabel = resumo.PrecoMedioLitro is decimal preco ? $"{preco.ToString("C3", PtBr)}/L" : "—";
        CustoPorKmLabel = resumo.CustoPorKm is decimal custoKm ? $"{custoKm.ToString("C2", PtBr)}/km" : "—";
        CustoMedioAbastecimentoLabel = resumo.CustoMedioAbastecimento is decimal custoMedio ? custoMedio.ToString("C2", PtBr) : "—";

        var plural = resumo.Quantidade == 1 ? "abastecimento" : "abastecimentos";
        TotaisLabel = $"{resumo.TotalLitros.ToString("N2", PtBr)} L · {resumo.TotalGasto.ToString("C2", PtBr)} em {resumo.Quantidade} {plural}";

        SemMediaLabel = resumo.MediaKmL is null && resumo.Quantidade > 0
            ? "A média aparece a partir do segundo abastecimento com tanque cheio."
            : null;

        MediasPorVeiculo = new ObservableCollection<ConsumoVeiculoRowViewModel>(
            (consumo?.Veiculos ?? new List<ConsumoVeiculo>()).Select(v => new ConsumoVeiculoRowViewModel(v, PtBr)));
    }

    [RelayCommand]
    private async Task AddAbastecimentoAsync()
    {
        var parametros = new Dictionary<string, object>();
        if (SelectedFiltroVeiculo?.Id is int veiculoId) parametros["veiculoId"] = veiculoId;

        await Shell.Current.GoToAsync(nameof(AbastecimentoFormPage), parametros);
    }

    [RelayCommand]
    private async Task EditAbastecimentoAsync(AbastecimentoRowViewModel? row)
    {
        if (row is null) return;

        await Shell.Current.GoToAsync(nameof(AbastecimentoFormPage), new Dictionary<string, object>
        {
            ["abastecimento"] = row.Original,
        });
    }
}
