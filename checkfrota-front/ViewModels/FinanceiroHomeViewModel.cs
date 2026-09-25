using checkfrota_front.Services;
using checkfrota_front.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace checkfrota_front.ViewModels;

public partial class FinanceiroHomeViewModel : AdminSectionViewModelBase
{
    public FinanceiroHomeViewModel(ApiAuthService apiAuth) : base(apiAuth)
    {
    }

    [RelayCommand]
    private async Task AppearingAsync()
    {
        await LoadAvatarAsync();
    }

    [RelayCommand]
    private async Task AbrirCategoriasAsync()
    {
        await Shell.Current.GoToAsync(nameof(CategoriaFinanceiraManagementPage));
    }

    [RelayCommand]
    private async Task AbrirFormasPagamentoAsync()
    {
        await Shell.Current.GoToAsync(nameof(FormaPagamentoManagementPage));
    }
}
