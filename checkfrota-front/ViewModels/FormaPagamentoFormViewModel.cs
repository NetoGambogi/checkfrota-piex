using checkfrota_front.Models;
using checkfrota_front.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace checkfrota_front.ViewModels;

public partial class FormaPagamentoFormViewModel : AdminSectionViewModelBase, IQueryAttributable
{
    private readonly FormaPagamentoService _formaService;

    private int? _formaId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageTitle))]
    private bool isEditing;

    public string PageTitle => IsEditing ? "Editar forma de pagamento" : "Nova forma de pagamento";

    [ObservableProperty]
    private string nome = string.Empty;

    /// <summary>Mesmo limite da API (NOME_MAXIMO).</summary>
    public const int NomeMaximo = 50;

    /// <summary>Registros antigos podem ter nome maior: o campo aceita o tamanho atual para não cortar o texto.</summary>
    [ObservableProperty]
    private int nomeMaxLength = NomeMaximo;

    private string? _nomeOriginal;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private string? errorMessage;

    public FormaPagamentoFormViewModel(FormaPagamentoService formaService, ApiAuthService apiAuth) : base(apiAuth)
    {
        _formaService = formaService;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("forma", out var formaObj) && formaObj is FormaPagamentoListItem forma)
        {
            _formaId = forma.Id;
            Nome = forma.Nome;
            _nomeOriginal = forma.Nome;
            NomeMaxLength = Math.Max(NomeMaximo, forma.Nome.Length);
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

        if (string.IsNullOrWhiteSpace(Nome))
        {
            ErrorMessage = "Informe o nome da forma de pagamento.";
            return;
        }

        if (Nome.Trim().Length > NomeMaximo && Nome.Trim() != _nomeOriginal?.Trim())
        {
            ErrorMessage = $"O nome deve ter no máximo {NomeMaximo} caracteres.";
            return;
        }

        try
        {
            BusyMessage = "Salvando…";
            IsBusy = true;
            ErrorMessage = null;

            var result = _formaId is int id
                ? await _formaService.UpdateAsync(id, Nome)
                : await _formaService.CreateAsync(Nome);

            if (result is null)
            {
                ErrorMessage = "Não foi possível salvar a forma de pagamento.";
                return;
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado ao salvar a forma de pagamento.";
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
        if (IsBusy || _formaId is not int id) return;

        var confirmar = await Shell.Current.DisplayAlertAsync(
            "Excluir forma de pagamento",
            $"Tem certeza que deseja excluir \"{Nome}\"?",
            "Excluir",
            "Cancelar");

        if (!confirmar) return;

        try
        {
            BusyMessage = "Excluindo…";
            IsBusy = true;
            var ok = await _formaService.DeleteAsync(id);
            if (!ok)
            {
                ErrorMessage = "Não foi possível excluir essa forma de pagamento.";
                return;
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado ao excluir a forma de pagamento.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
