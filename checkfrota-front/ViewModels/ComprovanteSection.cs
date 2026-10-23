using checkfrota_front.Helpers;
using checkfrota_front.Models;
using checkfrota_front.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace checkfrota_front.ViewModels;

/// <summary>
/// Comprovante (foto ou PDF da nota) dos lançamentos da frota que geram despesa: obrigatório ao
/// lançar uma despesa já paga e, na edição, pode ser trocado. O arquivo escolhido só é enviado ao
/// salvar o formulário. Exibido pela view <c>Views/Shared/ComprovanteField</c>.
/// </summary>
public partial class ComprovanteSection : ObservableObject
{
    private ComprovanteResumo? _salvo;

    /// <summary>Arquivo escolhido no aparelho, a enviar ao salvar.</summary>
    public FileResult? Pendente { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasComprovante))]
    [NotifyPropertyChangedFor(nameof(HasNoComprovante))]
    private string? nome;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanVer))]
    [NotifyPropertyChangedFor(nameof(CanRemover))]
    private bool isPendente;

    [ObservableProperty]
    private string? erro;

    public bool HasComprovante => Nome is not null;
    public bool HasNoComprovante => !HasComprovante;

    /// <summary>"Ver" abre o comprovante já salvo; o recém-escolhido ainda não está na API.</summary>
    public bool CanVer => !IsPendente && _salvo is not null;

    /// <summary>Só o arquivo recém-escolhido pode ser descartado; o salvo é substituído com "Trocar".</summary>
    public bool CanRemover => IsPendente;

    /// <summary>Comprovante que já está no lançamento (edição).</summary>
    public void Carregar(ComprovanteResumo? salvo)
    {
        _salvo = salvo;
        Pendente = null;
        IsPendente = false;
        Nome = salvo?.Nome;
        OnPropertyChanged(nameof(CanVer));
    }

    /// <summary>Mensagem de erro quando o comprovante obrigatório não foi anexado.</summary>
    public string? Validar(bool obrigatorio) =>
        obrigatorio && Pendente is null && _salvo is null
            ? "Anexe a foto ou o PDF do comprovante."
            : null;

    [RelayCommand]
    private async Task AnexarAsync()
    {
        try
        {
            var arquivo = await Anexos.EscolherComprovanteAsync();
            if (arquivo is null) return;

            if (await Anexos.ValidarAsync(arquivo) is string mensagem)
            {
                Erro = mensagem;
                return;
            }

            Erro = null;
            Pendente = arquivo;
            IsPendente = true;
            Nome = arquivo.FileName;
        }
        catch (PermissionException)
        {
            Erro = "Permita o acesso à câmera para fotografar o comprovante.";
        }
        catch (Exception ex)
        {
            Erro = "Não foi possível selecionar o comprovante.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    [RelayCommand]
    private void Remover() => Carregar(_salvo);

    [RelayCommand]
    private async Task VerAsync()
    {
        if (_salvo is null) return;

        await Shell.Current.GoToAsync(nameof(ComprovanteViewerPage), new Dictionary<string, object>
        {
            ["comprovante"] = _salvo,
        });
    }
}
