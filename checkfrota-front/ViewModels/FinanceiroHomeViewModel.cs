using checkfrota_front.Services;
using checkfrota_front.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;

namespace checkfrota_front.ViewModels;

public partial class FinanceiroHomeViewModel : AdminSectionViewModelBase
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private readonly MovimentacaoFinanceiraService _movimentacaoService;

    [ObservableProperty]
    private string saldoAtualFormatado = "—";

    [ObservableProperty]
    private string saldoPrevistoFormatado = "—";

    public FinanceiroHomeViewModel(MovimentacaoFinanceiraService movimentacaoService, ApiAuthService apiAuth) : base(apiAuth)
    {
        _movimentacaoService = movimentacaoService;
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        await LoadAvatarAsync();
        await LoadResumoAsync();
    }

    private async Task LoadResumoAsync()
    {
        try
        {
            var resumo = await _movimentacaoService.GetResumoAsync();
            if (resumo is null) return;

            SaldoAtualFormatado = resumo.SaldoAtual.ToString("C2", PtBr);
            SaldoPrevistoFormatado = resumo.SaldoPrevisto.ToString("C2", PtBr);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    [RelayCommand]
    private async Task AbrirCategoriasAsync()
    {
        await Shell.Current.GoToAsync(nameof(CategoriaFinanceiraManagementPage));
    }

    [RelayCommand]
    private async Task AbrirFormasPagamentoAsync()
    {
        await Shell.Current.GoToAsync(nameof(FormaPagamentoManagementPage));
    }

    [RelayCommand]
    private async Task AbrirMovimentacoesAsync()
    {
        await Shell.Current.GoToAsync(nameof(MovimentacaoFinanceiraManagementPage));
    }

    [RelayCommand]
    private async Task AbrirFinanciamentosAsync()
    {
        await Shell.Current.GoToAsync(nameof(FinanciamentoManagementPage));
    }
}
