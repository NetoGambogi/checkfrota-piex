using checkfrota_front.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace checkfrota_front.ViewModels;

public partial class NotificacoesViewModel : AdminSectionViewModelBase
{
    private readonly NotificacaoService _notificacaoService;

    private bool _isLoadingNotificacoes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInitialLoading))]
    private ObservableCollection<NotificacaoRowViewModel> notificacoes = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    [NotifyPropertyChangedFor(nameof(ShowInitialLoading))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    private bool hasNoResults;

    [ObservableProperty]
    private string? errorMessage;

    /// <summary>Abrindo uma notificação ou marcando todas como lidas (o IsBusy é do carregamento da lista).</summary>
    [ObservableProperty]
    private bool isProcessando;

    public bool ShowEmptyState => HasNoResults && !IsBusy;
    public bool ShowInitialLoading => IsBusy && Notificacoes.Count == 0;

    public NotificacaoService Central => _notificacaoService;

    public NotificacoesViewModel(NotificacaoService notificacaoService, ApiAuthService apiAuth) : base(apiAuth)
    {
        _notificacaoService = notificacaoService;
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        await LoadAvatarAsync();
        await LoadNotificacoesAsync();
    }

    [RelayCommand]
    private async Task LoadNotificacoesAsync()
    {
        if (_isLoadingNotificacoes) return;

        try
        {
            _isLoadingNotificacoes = true;
            IsBusy = true;
            ErrorMessage = null;

            var items = await _notificacaoService.ListarAsync();
            Notificacoes = new ObservableCollection<NotificacaoRowViewModel>(items.Select(n => new NotificacaoRowViewModel(n)));
            HasNoResults = Notificacoes.Count == 0;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível carregar as notificações.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
            _isLoadingNotificacoes = false;
        }
    }

    [RelayCommand]
    private async Task AbrirAsync(NotificacaoRowViewModel? row)
    {
        if (row is null || IsProcessando) return;

        try
        {
            BusyMessage = "Abrindo…";
            IsProcessando = true;

            if (row.NaoLida)
            {
                row.Lida = true;
                await _notificacaoService.MarcarComoLidaAsync(row.Id);
            }

            await _notificacaoService.AbrirDestinoAsync(row.Destino);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível abrir a notificação.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsProcessando = false;
        }
    }

    [RelayCommand]
    private async Task MarcarTodasComoLidasAsync()
    {
        if (IsProcessando) return;

        try
        {
            BusyMessage = "Marcando como lidas…";
            IsProcessando = true;

            await _notificacaoService.MarcarTodasComoLidasAsync();

            foreach (var row in Notificacoes)
                row.Lida = true;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Não foi possível marcar as notificações como lidas.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsProcessando = false;
        }
    }
}
