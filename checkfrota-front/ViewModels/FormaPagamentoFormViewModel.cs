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

        try
        {
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
