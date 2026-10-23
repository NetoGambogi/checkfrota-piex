using checkfrota_front.Services;
using checkfrota_front.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    public bool ShowEmptyState => HasNoResults && !IsBusy;
    public bool ShowInitialLoading => IsBusy && Abastecimentos.Count == 0;

    public MeusAbastecimentosViewModel(MotoristaService motoristaService, ApiAuthService apiAuth) : base(apiAuth)
    {
        _motoristaService = motoristaService;
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

            var items = await _motoristaService.GetAbastecimentosAsync();
            Abastecimentos = new ObservableCollection<AbastecimentoRowViewModel>(items.Select(a => new AbastecimentoRowViewModel(a)));
            HasNoResults = Abastecimentos.Count == 0;
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
        if (row?.Original.Comprovante is not { } comprovante) return;

        await Shell.Current.GoToAsync(nameof(ComprovanteViewerPage), new Dictionary<string, object>
        {
            ["comprovante"] = comprovante,
        });
    }
}
