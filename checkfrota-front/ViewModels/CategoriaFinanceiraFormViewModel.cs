using checkfrota_front.Models;
using checkfrota_front.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace checkfrota_front.ViewModels;

public class CategoriaIconeOption
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public partial class CategoriaFinanceiraFormViewModel : AdminSectionViewModelBase, IQueryAttributable
{
    private readonly CategoriaFinanceiraService _categoriaService;

    public static List<CategoriaIconeOption> IconOptions { get; } = new()
    {
        new CategoriaIconeOption { Key = "combustivel", Label = "Combustível" },
        new CategoriaIconeOption { Key = "manutencao", Label = "Manutenção" },
        new CategoriaIconeOption { Key = "pedagio", Label = "Pedágio" },
        new CategoriaIconeOption { Key = "seguro", Label = "Seguro" },
        new CategoriaIconeOption { Key = "outros", Label = "Outros" },
    };

    private int? _categoriaId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageTitle))]
    private bool isEditing;

    public string PageTitle => IsEditing ? "Editar categoria" : "Nova categoria";

    public List<CategoriaIconeOption> Icones => IconOptions;

    [ObservableProperty]
    private string nome = string.Empty;

    [ObservableProperty]
    private string? descricao;

    [ObservableProperty]
    private string tipo = "despesa";

    [ObservableProperty]
    private CategoriaIconeOption selectedIcone = IconOptions[^1];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private string? errorMessage;

    public CategoriaFinanceiraFormViewModel(CategoriaFinanceiraService categoriaService, ApiAuthService apiAuth) : base(apiAuth)
    {
        _categoriaService = categoriaService;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("categoria", out var categoriaObj) && categoriaObj is CategoriaFinanceiraListItem categoria)
        {
            _categoriaId = categoria.Id;
            Nome = categoria.Nome;
            Descricao = categoria.Descricao;
            Tipo = categoria.Tipo;
            SelectedIcone = IconOptions.FirstOrDefault(o => o.Key == categoria.Icone) ?? IconOptions[^1];
            IsEditing = true;
        }
        else if (query.TryGetValue("tipo", out var tipoObj) && tipoObj is string tipoInicial)
        {
            Tipo = tipoInicial;
        }
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        await LoadAvatarAsync();
    }

    [RelayCommand]
    private void SelectDespesa() => Tipo = "despesa";

    [RelayCommand]
    private void SelectReceita() => Tipo = "receita";

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;

        if (string.IsNullOrWhiteSpace(Nome))
        {
            ErrorMessage = "Informe o nome da categoria.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var result = _categoriaId is int id
                ? await _categoriaService.UpdateAsync(id, Nome, Tipo, Descricao, SelectedIcone.Key)
                : await _categoriaService.CreateAsync(Nome, Tipo, Descricao, SelectedIcone.Key);

            if (result is null)
            {
                ErrorMessage = "Não foi possível salvar a categoria.";
                return;
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado ao salvar a categoria.";
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
        if (IsBusy || _categoriaId is not int id) return;

        var confirmar = await Shell.Current.DisplayAlertAsync(
            "Excluir categoria",
            $"Tem certeza que deseja excluir \"{Nome}\"?",
            "Excluir",
            "Cancelar");

        if (!confirmar) return;

        try
        {
            IsBusy = true;
            var ok = await _categoriaService.DeleteAsync(id);
            if (!ok)
            {
                ErrorMessage = "Não foi possível excluir essa categoria.";
                return;
            }

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado ao excluir a categoria.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
