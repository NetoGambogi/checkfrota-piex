using checkfrota_front.Models;
using checkfrota_front.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace checkfrota_front.ViewModels;

public partial class RotaFormViewModel : AdminSectionViewModelBase, IQueryAttributable
{
    private readonly RotaService _rotaService;

    private int? _rotaId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageTitle))]
    private bool isEditing;

    public string PageTitle => IsEditing ? "Editar rota" : "Nova rota";

    [ObservableProperty]
    private string nome = string.Empty;

    [ObservableProperty]
    private string origem = string.Empty;

    [ObservableProperty]
    private string destino = string.Empty;

    [ObservableProperty]
    private string kmAproximado = string.Empty;

    [ObservableProperty]
    private string? observacoes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private string? errorMessage;

    public RotaFormViewModel(RotaService rotaService, ApiAuthService apiAuth) : base(apiAuth)
    {
        _rotaService = rotaService;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("rota", out var rotaObj) && rotaObj is RotaListItem rota)
        {
            _rotaId = rota.Id;
            Nome = rota.Nome;
            Origem = rota.Origem;
            Destino = rota.Destino;
            KmAproximado = rota.KmAproximado.ToString();
            Observacoes = rota.Observacoes;
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

        if (string.IsNullOrWhiteSpace(Origem) || string.IsNullOrWhiteSpace(Destino))
        {
            ErrorMessage = "Informe a origem e o destino.";
            return;
        }

        if (!int.TryParse(KmAproximado.Replace(".", ""), out var km) || km < 1)
        {
            ErrorMessage = "Informe a distância aproximada em km.";
            return;
        }

        // Sem nome informado, a rota é identificada pelo próprio trajeto.
        var nomeFinal = string.IsNullOrWhiteSpace(Nome) ? $"{Origem.Trim()} x {Destino.Trim()}" : Nome.Trim();

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var resultado = await _rotaService.SaveAsync(
                _rotaId,
                nomeFinal,
                Origem.Trim(),
                Destino.Trim(),
                km,
                string.IsNullOrWhiteSpace(Observacoes) ? null : Observacoes.Trim());

            if (!resultado.Sucesso)
            {
                ErrorMessage = resultado.Erro ?? "Não foi possível salvar a rota.";
                return;
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado ao salvar a rota.";
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
        if (IsBusy || _rotaId is not int id) return;

        var confirmar = await Shell.Current.DisplayAlertAsync(
            "Excluir rota",
            $"Tem certeza que deseja excluir \"{Nome}\"?",
            "Excluir",
            "Cancelar");

        if (!confirmar) return;

        try
        {
            IsBusy = true;
            var ok = await _rotaService.DeleteAsync(id);
            if (!ok)
            {
                ErrorMessage = "Não foi possível excluir essa rota.";
                return;
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado ao excluir a rota.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
