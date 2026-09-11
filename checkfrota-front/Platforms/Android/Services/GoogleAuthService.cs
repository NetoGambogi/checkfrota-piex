using Android.OS;
using AndroidX.Core.Content;
using AndroidX.Credentials;
using Java.Interop;
using Xamarin.GoogleAndroid.Libraries.Identity.GoogleId;
using checkfrota_front.Services;

namespace checkfrota_front.Platforms.Android.Services;

public class GoogleAuthService : IGoogleAuthService
{
    private const string WebClientId = "890385103906-386698d2j71nbtj81g51jp0c9uenfhac.apps.googleusercontent.com";

    public Task<GoogleSignInResult?> SignInAsync()
    {
        var activity = Platform.CurrentActivity
            ?? throw new NullReferenceException("Nenhuma Activity atual encontrada.");

        var googleIdOption = new GetGoogleIdOption.Builder()
            .SetFilterByAuthorizedAccounts(false)
            .SetServerClientId(WebClientId)
            .SetAutoSelectEnabled(false)
            .Build();

        var request = new GetCredentialRequest.Builder()
            .AddCredentialOption(googleIdOption)
            .Build();

        var credentialManager = CredentialManager.Create(activity);
        var executor = ContextCompat.GetMainExecutor(activity)
            ?? throw new InvalidOperationException("Não foi possível obter o executor principal.");
        var tcs = new TaskCompletionSource<GoogleSignInResult?>();

        credentialManager.GetCredentialAsync(
            activity,
            request,
            cancellationSignal: null,
            executor,
            new CredentialCallback(tcs));

        return tcs.Task;
    }

    private sealed class CredentialCallback(TaskCompletionSource<GoogleSignInResult?> tcs)
        : Java.Lang.Object, ICredentialManagerCallback
    {
        public void OnResult(Java.Lang.Object? result)
        {
            var response = result?.JavaCast<GetCredentialResponse>();

            if (response?.Credential is CustomCredential custom
                && custom.Type == GoogleIdTokenCredential.TypeGoogleIdTokenCredential)
            {
                var googleIdTokenCredential = GoogleIdTokenCredential.CreateFrom(custom.Data);
                tcs.TrySetResult(new GoogleIdTokenResult(googleIdTokenCredential.IdToken));
            }
            else
            {
                tcs.TrySetResult(null);
            }
        }

        public void OnError(Java.Lang.Object e)
        {
            tcs.TrySetResult(null);
        }
    }
}
