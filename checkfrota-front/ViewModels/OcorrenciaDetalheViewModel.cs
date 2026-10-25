using checkfrota_front.Models;
using checkfrota_front.Services;
using checkfrota_front.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace checkfrota_front.ViewModels;

/// <summary>
/// Detalhe de uma ocorrência. O motorista só acompanha; a gestão da frota (admin/frota) também
/// resolve, com uma observação do que foi feito, ou reabre.
/// </summary>
public partial class OcorrenciaDetalheViewModel : AdminSectionViewModelBase, IQueryAttributable
{
    private readonly OcorrenciaService _ocorrenciaService;
    private readonly ApiAuthService _apiAuth;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanResolver))]
    [NotifyPropertyChangedFor(nameof(CanReabrir))]
    private OcorrenciaRowViewModel? ocorrencia;

    [ObservableProperty]
    private string? viagemLabel;

    [ObservableProperty]
    private string? resolucaoLabel;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanResolver))]
    [NotifyPropertyChangedFor(nameof(CanReabrir))]
    private bool isGestor;

    public bool CanResolver => IsGestor && Ocorrencia is { IsResolvida: false };
    public bool CanReabrir => IsGestor && Ocorrencia is { IsResolvida: true };

    [ObservableProperty]
    private string? observacaoResolucao;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    public bool IsNotBusy => !IsBusy;

    [ObservableProperty]
    private string? errorMessage;

    public OcorrenciaDetalheViewModel(OcorrenciaService ocorrenciaService, ApiAuthService apiAuth) : base(apiAuth)
    {
        _ocorrenciaService = ocorrenciaService;
        _apiAuth = apiAuth;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("ocorrencia", out var obj) && obj is OcorrenciaItem item)
        {
            Exibir(item);
        }
    }

    private void Exibir(OcorrenciaItem item)
    {
        Ocorrencia = new OcorrenciaRowViewModel(item);

        var rota = item.Viagem?.Rota;
        ViagemLabel = rota is null
            ? null
            : $"Viagem {rota.Nome} ({rota.Origem} → {rota.Destino})" + (item.Viagem?.Status == "concluida" ? " · concluída" : " · em andamento");

        ResolucaoLabel = item.Status == "resolvida"
            ? $"Resolvida por {item.ResolvidaPor?.Name ?? "-"} em {OcorrenciaRowViewModel.FormatarDataHora(item.ResolvidaEm)}"
              + (string.IsNullOrWhiteSpace(item.ObservacaoResolucao) ? "." : $": {item.ObservacaoResolucao}")
            : null;
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        await LoadAvatarAsync();
        IsGestor = await _apiAuth.GetRoleAsync() is "admin" or "frota";
    }

    [RelayCommand]
    private async Task AbrirFotoAsync()
    {
        if (Ocorrencia?.Original.Foto is not { } foto) return;

        // O visualizador de comprovantes já oferece zoom por pinça, útil para ver detalhes do problema.
        await Shell.Current.GoToAsync(nameof(ComprovanteViewerPage), new Dictionary<string, object>
        {
            ["comprovante"] = new ComprovanteResumo
            {
                Nome = "Foto da ocorrência",
                Url = foto.Url,
                Paginas = [foto.UrlExibicao],
            },
        });
    }

    [RelayCommand]
    private async Task ResolverAsync()
    {
        if (IsBusy || Ocorrencia is null) return;

        try
        {
            BusyMessage = "Resolvendo ocorrência…";
            IsBusy = true;
            ErrorMessage = null;

            var observacao = string.IsNullOrWhiteSpace(ObservacaoResolucao) ? null : ObservacaoResolucao.Trim();
            var resultado = await _ocorrenciaService.ResolverAsync(Ocorrencia.Original.Id, observacao);

            if (resultado.Item is not { } atualizada)
            {
                ErrorMessage = resultado.Erro ?? "Não foi possível resolver a ocorrência.";
                return;
            }

            ObservacaoResolucao = null;
            Exibir(atualizada);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado ao resolver a ocorrência.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ReabrirAsync()
    {
        if (IsBusy || Ocorrencia is null) return;

        var confirmar = await Shell.Current.DisplayAlertAsync(
            "Reabrir ocorrência",
            "A ocorrência volta para a lista de abertas e a resolução registrada é descartada.",
            "Reabrir",
            "Cancelar");

        if (!confirmar) return;

        try
        {
            BusyMessage = "Reabrindo ocorrência…";
            IsBusy = true;
            ErrorMessage = null;

            var resultado = await _ocorrenciaService.ReabrirAsync(Ocorrencia.Original.Id);

            if (resultado.Item is not { } atualizada)
            {
                ErrorMessage = resultado.Erro ?? "Não foi possível reabrir a ocorrência.";
                return;
            }

            Exibir(atualizada);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado ao reabrir a ocorrência.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
