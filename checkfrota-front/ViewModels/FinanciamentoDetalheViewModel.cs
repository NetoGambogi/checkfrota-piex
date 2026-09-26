using checkfrota_front.Models;
using checkfrota_front.Services;
using checkfrota_front.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;

namespace checkfrota_front.ViewModels;

public partial class FinanciamentoDetalheViewModel : AdminSectionViewModelBase, IQueryAttributable
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private readonly FinanciamentoService _financiamentoService;

    private int _financiamentoId;

    [ObservableProperty]
    private string descricao = string.Empty;

    [ObservableProperty]
    private string categoriaNome = string.Empty;

    [ObservableProperty]
    private string valorTotalFormatado = string.Empty;

    [ObservableProperty]
    private string progressoLabel = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInitialLoading))]
    private ObservableCollection<MovimentacaoFinanceiraRowViewModel> parcelas = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInitialLoading))]
    private bool isBusy;

    [ObservableProperty]
    private string? errorMessage;

    public bool ShowInitialLoading => IsBusy && Parcelas.Count == 0;

    public FinanciamentoDetalheViewModel(FinanciamentoService financiamentoService, ApiAuthService apiAuth) : base(apiAuth)
    {
        _financiamentoService = financiamentoService;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("financiamentoId", out var idObj) && idObj is int id)
        {
            _financiamentoId = id;
        }
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        await LoadAvatarAsync();
        await LoadDetalheAsync();
    }

    [RelayCommand]
    private async Task LoadDetalheAsync()
    {
        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var detalhe = await _financiamentoService.GetDetalheAsync(_financiamentoId);
            if (detalhe is null)
            {
                ErrorMessage = "Não foi possível carregar o financiamento.";
                return;
            }

            Descricao = detalhe.Financiamento.Descricao;
            CategoriaNome = detalhe.Financiamento.Categoria?.Nome ?? "-";
            ValorTotalFormatado = detalhe.Financiamento.ValorTotal.ToString("C2", PtBr);
            ProgressoLabel = $"{detalhe.Financiamento.ParcelasPagas ?? 0}/{detalhe.Financiamento.QuantidadeParcelas} parcelas pagas";
            Parcelas = new ObservableCollection<MovimentacaoFinanceiraRowViewModel>(
                detalhe.Parcelas.Select(p => new MovimentacaoFinanceiraRowViewModel(p)));
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível carregar o financiamento.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AbrirParcelaAsync(MovimentacaoFinanceiraRowViewModel? row)
    {
        if (row is null) return;

        await Shell.Current.GoToAsync(nameof(MovimentacaoFinanceiraFormPage), new Dictionary<string, object>
        {
            ["movimentacao"] = row.Original,
        });
    }
}
