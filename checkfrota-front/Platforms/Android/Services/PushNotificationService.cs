using Android.Gms.Extensions;
using checkfrota_front.Services;
using Firebase;
using Firebase.Messaging;

namespace checkfrota_front.Platforms.Android.Services;

public class PushNotificationService : IPushNotificationService
{
    public string Plataforma => "android";

    public async Task<string?> ObterTokenAsync()
    {
        // Sem o google-services.json o Firebase não é inicializado e o app segue sem push.
        if (FirebaseApp.GetApps(Platform.AppContext).Count == 0)
            return null;

        // Android 13+ pede a permissão; nas versões anteriores já vem concedida.
        await MainThread.InvokeOnMainThreadAsync(Permissions.RequestAsync<Permissions.PostNotifications>);

        // Obsoleto só no binding; getToken() é a forma atual de obter o token no FCM.
#pragma warning disable CS0618
        var token = await FirebaseMessaging.Instance.GetToken().AsAsync<Java.Lang.String>();
#pragma warning restore CS0618
        return token?.ToString();
    }
}
