using checkfrota_front.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace checkfrota_front.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IGoogleAuthService _googleAuth;
    private readonly ApiAuthService _apiAuth;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string? errorMessage;

    public LoginViewModel(IGoogleAuthService googleAuth, ApiAuthService apiAuth)
    {
        _googleAuth = googleAuth;
        _apiAuth = apiAuth;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            ErrorMessage = null;

            var idToken = await _googleAuth.SignInAsync();
            if (idToken is null)
            {
                ErrorMessage = "Login cancelado.";
                return;
            }

            var login = await _apiAuth.LoginWithGoogleAsync(idToken);
            if (login is null)
            {
                ErrorMessage = "Não foi possível autenticar. Tente novamente.";
                return;
            }

            // Roteamento por role
            var route = login.User.Role switch
            {
                "admin" => "//admin",
                "motorista" => "//motorista",
                _ => throw new InvalidOperationException($"Role desconhecida: {login.User.Role}")
            };

            await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            ErrorMessage = "Erro inesperado ao fazer login.";
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}