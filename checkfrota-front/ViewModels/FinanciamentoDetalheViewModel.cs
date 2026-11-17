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
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    public bool IsNotBusy => !IsBusy;

    /// <summary>Só dá para excluir depois que o financiamento carregou (a confirmação usa os dados dele).</summary>
    [ObservableProperty]
    private bool canExcluir;

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

            var parcelasPagas = detalhe.Parcelas.Count(p => p.Status == "pago");
            ProgressoLabel = $"{parcelasPagas}/{detalhe.Financiamento.QuantidadeParcelas} parcelas pagas";

            Parcelas = new ObservableCollection<MovimentacaoFinanceiraRowViewModel>(
                detalhe.Parcelas.Select(p => new MovimentacaoFinanceiraRowViewModel(p)));
            CanExcluir = detalhe.Financiamento.Ativo;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Não foi possível carregar o financiamento. ({ex.Message})";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Exclui o financiamento inteiro, com todas as parcelas (inclusive as já pagas). Parcelas não
    /// são excluídas uma a uma: um financiamento lançado errado sai por completo.
    /// </summary>
    [RelayCommand]
    private async Task ExcluirAsync()
    {
        if (IsBusy || !CanExcluir) return;

        var parcelasPagas = Parcelas.Count(p => p.IsPago);
        var avisoPagas = parcelasPagas > 0
            ? $" As {parcelasPagas} parcela(s) já paga(s) também serão excluídas do financeiro."
            : string.Empty;

        var confirmar = await Shell.Current.DisplayAlertAsync(
            "Excluir financiamento",
            $"Tem certeza que deseja excluir \"{Descricao}\" e todas as suas {Parcelas.Count} parcelas?{avisoPagas}",
            "Excluir",
            "Cancelar");

        if (!confirmar) return;

        try
        {
            BusyMessage = "Excluindo…";
            IsBusy = true;
            ErrorMessage = null;

            if (!await _financiamentoService.DeleteAsync(_financiamentoId))
            {
                ErrorMessage = "Não foi possível excluir esse financiamento.";
                return;
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado ao excluir o financiamento.";
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
