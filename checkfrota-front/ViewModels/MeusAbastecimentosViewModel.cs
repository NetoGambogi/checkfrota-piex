using checkfrota_front.Services;
using checkfrota_front.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;

namespace checkfrota_front.ViewModels;

public partial class MeusAbastecimentosViewModel : AdminSectionViewModelBase
{
    private readonly MotoristaService _motoristaService;

    // Evita a recarga sem fim do RefreshView (ver ViagemManagementViewModel).
    private bool _isLoadingAbastecimentos;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInitialLoading))]
    private ObservableCollection<AbastecimentoRowViewModel> abastecimentos = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    [NotifyPropertyChangedFor(nameof(ShowInitialLoading))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool hasNoResults;

    [ObservableProperty]
    private string? errorMessage;

    /// <summary>Sem rede: a lista vem do que ficou salvo no aparelho.</summary>
    [ObservableProperty]
    private bool isOffline;

    public bool ShowEmptyState => HasNoResults && !IsBusy;
    public bool ShowInitialLoading => IsBusy && Abastecimentos.Count == 0;

    public MeusAbastecimentosViewModel(MotoristaService motoristaService, ApiAuthService apiAuth) : base(apiAuth)
    {
        _motoristaService = motoristaService;

        // Um registro enviado (ou recusado) em segundo plano atualiza a lista aberta.
        WeakReferenceMessenger.Default.Register<MeusAbastecimentosViewModel, FilaOfflineAlterada>(this,
            (vm, _) => MainThread.BeginInvokeOnMainThread(() => vm.LoadAbastecimentosCommand.Execute(null)));
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        await LoadAvatarAsync();
        await LoadAbastecimentosAsync();
    }

    [RelayCommand]
    private async Task LoadAbastecimentosAsync()
    {
        if (_isLoadingAbastecimentos) return;

        try
        {
            _isLoadingAbastecimentos = true;
            IsBusy = true;
            ErrorMessage = null;
            IsOffline = Connectivity.Current.NetworkAccess != NetworkAccess.Internet;

            var pendentes = await _motoristaService.GetAbastecimentosPendentesAsync();
            var rows = pendentes
                .OrderByDescending(p => p.CriadaEm)
                .Where(p => p.Abastecimento is not null)
                .Select(p => new AbastecimentoRowViewModel(p.Abastecimento!.ComoItemDaLista(p.Id), p))
                .ToList();

            try
            {
                var pendentesIds = pendentes.Select(p => p.Id.ToString()).ToHashSet();
                var items = await _motoristaService.GetAbastecimentosAsync();
                rows.AddRange(items
                    .Where(a => a.ClientUuid is null || !pendentesIds.Contains(a.ClientUuid))
                    .Select(a => new AbastecimentoRowViewModel(a)));
            }
            catch (Exception ex)
            {
                // Sem rede e sem nada salvo: ainda mostra os registros feitos no aparelho.
                ErrorMessage = "Não foi possível carregar seus abastecimentos.";
                System.Diagnostics.Debug.WriteLine(ex);
            }

            Abastecimentos = new ObservableCollection<AbastecimentoRowViewModel>(rows);
            HasNoResults = Abastecimentos.Count == 0 && ErrorMessage is null;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível carregar seus abastecimentos.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
            _isLoadingAbastecimentos = false;
        }
    }

    [RelayCommand]
    private Task RegistrarAbastecimentoAsync() => Shell.Current.GoToAsync(nameof(MotoristaAbastecimentoFormPage));

    [RelayCommand]
    private async Task AbrirComprovanteAsync(AbastecimentoRowViewModel? row)
    {
        if (row?.Pendencia is { } pendencia)
        {
            await ResolverPendenciaAsync(pendencia);
            return;
        }

        if (row?.Original.Comprovante is not { } comprovante) return;

        await Shell.Current.GoToAsync(nameof(ComprovanteViewerPage), new Dictionary<string, object>
        {
            ["comprovante"] = comprovante,
        });
    }

    private async Task ResolverPendenciaAsync(OperacaoPendente pendencia)
    {
        if (pendencia.Situacao == SituacaoOperacao.Pendente)
        {
            await Shell.Current.DisplayAlertAsync(
                "Aguardando envio",
                "Este abastecimento está salvo no aparelho e será enviado automaticamente quando houver internet.",
                "OK");
            return;
        }

        var descartar = await Shell.Current.DisplayAlertAsync(
            "Abastecimento não enviado",
            $"{pendencia.Erro}\n\nDescarte este registro e lance de novo com os dados corrigidos.",
            "Descartar",
            "Manter");

        if (descartar)
        {
            await _motoristaService.DescartarPendenciaAsync(pendencia.Id);
        }
    }
}
