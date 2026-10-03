using checkfrota_front.Models;
using checkfrota_front.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace checkfrota_front.ViewModels;

public class VeiculoTipoOption
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public partial class VeiculoFormViewModel : AdminSectionViewModelBase, IQueryAttributable
{
    private readonly VeiculoService _veiculoService;

    public static List<VeiculoTipoOption> TipoOptions { get; } = new()
    {
        new VeiculoTipoOption { Key = "caminhao", Label = "Caminhão" },
        new VeiculoTipoOption { Key = "carreta", Label = "Carreta" },
        new VeiculoTipoOption { Key = "utilitario", Label = "Utilitário" },
        new VeiculoTipoOption { Key = "carro", Label = "Carro" },
        new VeiculoTipoOption { Key = "moto", Label = "Moto" },
        new VeiculoTipoOption { Key = "outro", Label = "Outro" },
    };

    private int? _veiculoId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageTitle))]
    private bool isEditing;

    public string PageTitle => IsEditing ? "Editar veículo" : "Novo veículo";

    public List<VeiculoTipoOption> Tipos => TipoOptions;

    [ObservableProperty]
    private string placa = string.Empty;

    [ObservableProperty]
    private VeiculoTipoOption selectedTipo = TipoOptions[0];

    [ObservableProperty]
    private string marca = string.Empty;

    [ObservableProperty]
    private string modelo = string.Empty;

    [ObservableProperty]
    private string ano = string.Empty;

    [ObservableProperty]
    private string? renavam;

    [ObservableProperty]
    private string kmAtual = string.Empty;

    [ObservableProperty]
    private string? observacoes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private string? errorMessage;

    public VeiculoFormViewModel(VeiculoService veiculoService, ApiAuthService apiAuth) : base(apiAuth)
    {
        _veiculoService = veiculoService;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("veiculo", out var veiculoObj) && veiculoObj is VeiculoListItem veiculo)
        {
            _veiculoId = veiculo.Id;
            Placa = veiculo.Placa;
            SelectedTipo = TipoOptions.FirstOrDefault(t => t.Key == veiculo.Tipo) ?? TipoOptions[^1];
            Marca = veiculo.Marca;
            Modelo = veiculo.Modelo;
            Ano = veiculo.Ano?.ToString() ?? string.Empty;
            Renavam = veiculo.Renavam;
            KmAtual = veiculo.KmAtual.ToString();
            Observacoes = veiculo.Observacoes;
            IsEditing = true;
        }
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        await LoadAvatarAsync();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;

        if (string.IsNullOrWhiteSpace(Placa))
        {
            ErrorMessage = "Informe a placa do veículo.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Marca) || string.IsNullOrWhiteSpace(Modelo))
        {
            ErrorMessage = "Informe a marca e o modelo.";
            return;
        }

        int? anoInt = null;
        if (!string.IsNullOrWhiteSpace(Ano))
        {
            if (!int.TryParse(Ano, out var anoValor))
            {
                ErrorMessage = "Informe um ano válido.";
                return;
            }
            anoInt = anoValor;
        }

        var km = 0;
        if (!string.IsNullOrWhiteSpace(KmAtual) && (!int.TryParse(KmAtual.Replace(".", ""), out km) || km < 0))
        {
            ErrorMessage = "Informe uma quilometragem válida.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var resultado = await _veiculoService.SaveAsync(_veiculoId, new VeiculoListItem
            {
                Placa = Placa.Trim(),
                Tipo = SelectedTipo.Key,
                Marca = Marca.Trim(),
                Modelo = Modelo.Trim(),
                Ano = anoInt,
                Renavam = string.IsNullOrWhiteSpace(Renavam) ? null : Renavam.Trim(),
                KmAtual = km,
                Observacoes = string.IsNullOrWhiteSpace(Observacoes) ? null : Observacoes.Trim(),
            });

            if (!resultado.Sucesso)
            {
                ErrorMessage = resultado.Erro ?? "Não foi possível salvar o veículo.";
                return;
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado ao salvar o veículo.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (IsBusy || _veiculoId is not int id) return;

        var confirmar = await Shell.Current.DisplayAlertAsync(
            "Excluir veículo",
            $"Tem certeza que deseja excluir o veículo {Placa}? O histórico de manutenções é mantido.",
            "Excluir",
            "Cancelar");

        if (!confirmar) return;

        try
        {
            IsBusy = true;
            var ok = await _veiculoService.DeleteAsync(id);
            if (!ok)
            {
                ErrorMessage = "Não foi possível excluir esse veículo.";
                return;
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado ao excluir o veículo.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
