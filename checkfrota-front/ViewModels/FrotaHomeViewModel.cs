using checkfrota_front.Services;
using checkfrota_front.Views;
using CommunityToolkit.Mvvm.Input;

namespace checkfrota_front.ViewModels;

public partial class FrotaHomeViewModel : AdminSectionViewModelBase
{
    public FrotaHomeViewModel(ApiAuthService apiAuth) : base(apiAuth)
    {
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        await LoadAvatarAsync();
    }

    [RelayCommand]
    private async Task AbrirVeiculosAsync()
    {
        await Shell.Current.GoToAsync(nameof(VeiculoManagementPage));
    }

    [RelayCommand]
    private async Task AbrirManutencoesAsync()
    {
        await Shell.Current.GoToAsync(nameof(ManutencaoManagementPage));
    }

    [RelayCommand]
    private async Task AbrirRotasAsync()
    {
        await Shell.Current.GoToAsync(nameof(RotaManagementPage));
    }
}
