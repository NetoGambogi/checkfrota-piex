using checkfrota_front.Models;
using checkfrota_front.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;

namespace checkfrota_front.ViewModels;

public partial class FinanciamentoFormViewModel : AdminSectionViewModelBase
{
    private static readonly FormaPagamentoListItem NenhumaForma = new() { Id = 0, Nome = "Nenhuma" };

    private readonly FinanciamentoService _financiamentoService;
    private readonly CategoriaFinanceiraService _categoriaService;
    private readonly FormaPagamentoService _formaPagamentoService;

    [ObservableProperty]
    private string descricao = string.Empty;

    [ObservableProperty]
    private ObservableCollection<CategoriaFinanceiraListItem> categorias = new();

    [ObservableProperty]
    private CategoriaFinanceiraListItem? selectedCategoria;

    [ObservableProperty]
    private List<FormaPagamentoListItem> formasPagamento = new();

    [ObservableProperty]
    private FormaPagamentoListItem selectedFormaPagamento = NenhumaForma;

    [ObservableProperty]
    private string valorParcela = string.Empty;

    [ObservableProperty]
    private string quantidadeParcelas = string.Empty;

    [ObservableProperty]
    private string diaVencimento = string.Empty;

    [ObservableProperty]
    private DateTime dataInicio = DateTime.Today;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private string? errorMessage;

    public FinanciamentoFormViewModel(
        FinanciamentoService financiamentoService,
        CategoriaFinanceiraService categoriaService,
        FormaPagamentoService formaPagamentoService,
        ApiAuthService apiAuth) : base(apiAuth)
    {
        _financiamentoService = financiamentoService;
        _categoriaService = categoriaService;
        _formaPagamentoService = formaPagamentoService;
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        await LoadAvatarAsync();

        var categoriasDespesa = await _categoriaService.GetCategoriasAsync("despesa");
        Categorias = new ObservableCollection<CategoriaFinanceiraListItem>(categoriasDespesa);
        SelectedCategoria = Categorias.FirstOrDefault();

        var formas = await _formaPagamentoService.GetFormasAsync();
        var options = new List<FormaPagamentoListItem> { NenhumaForma };
        options.AddRange(formas);
        FormasPagamento = options;
        SelectedFormaPagamento = NenhumaForma;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;

        if (string.IsNullOrWhiteSpace(Descricao))
        {
            ErrorMessage = "Informe a descrição do financiamento.";
            return;
        }

        if (SelectedCategoria is null)
        {
            ErrorMessage = "Selecione uma categoria de despesa.";
            return;
        }

        if (!decimal.TryParse(ValorParcela, NumberStyles.Number, CultureInfo.InvariantCulture, out var valorParcelaDecimal) || valorParcelaDecimal <= 0)
        {
            ErrorMessage = "Informe um valor de parcela válido.";
            return;
        }

        if (!int.TryParse(QuantidadeParcelas, out var quantidade) || quantidade < 1)
        {
            ErrorMessage = "Informe a quantidade de parcelas.";
            return;
        }

        if (!int.TryParse(DiaVencimento, out var dia) || dia < 1 || dia > 31)
        {
            ErrorMessage = "Informe um dia de vencimento entre 1 e 31.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var formaPagamentoId = SelectedFormaPagamento.Id > 0 ? SelectedFormaPagamento.Id : (int?)null;

            var resultado = await _financiamentoService.CreateAsync(
                Descricao,
                SelectedCategoria.Id,
                formaPagamentoId,
                valorParcelaDecimal,
                quantidade,
                dia,
                DataInicio);

            if (resultado is null)
            {
                ErrorMessage = "Não foi possível salvar o financiamento.";
                return;
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado ao salvar o financiamento.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
