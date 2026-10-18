using Android.App;
using checkfrota_front.Services;
using Firebase.Messaging;

namespace checkfrota_front.Platforms.Android.Services;

[Service(Exported = false)]
[IntentFilter(new[] { "com.google.firebase.MESSAGING_EVENT" })]
public class CheckFrotaMessagingService : FirebaseMessagingService
{
    // O binding do Firebase marca OnNewToken como obsoleto, mas é o callback atual do FCM para
    // troca de token (não há substituto na API Java).
#pragma warning disable CS0618, CS0672
    public override void OnNewToken(string token)
    {
        base.OnNewToken(token);
#pragma warning restore CS0618, CS0672

        var notificacoes = Notificacoes();
        if (notificacoes is not null)
            _ = notificacoes.AtualizarTokenAsync(token);
    }

    // Só é chamado com o app em primeiro plano. Em segundo plano ou fechado, o próprio Android
    // exibe o push e o toque abre o MainActivity com o bloco "data" nos extras.
    public override void OnMessageReceived(RemoteMessage message)
    {
        base.OnMessageReceived(message);

        var notificacao = message.GetNotification();
        NotificacoesAndroid.Exibir(this, notificacao?.Title, notificacao?.Body, message.Data);

        var notificacoes = Notificacoes();
        if (notificacoes is not null)
            _ = notificacoes.AtualizarContadorAsync();
    }

    private static NotificacaoService? Notificacoes() =>
        IPlatformApplication.Current?.Services.GetService<NotificacaoService>();
}
