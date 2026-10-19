using Android.App;
using Android.Content;
using AndroidX.Core.App;

namespace checkfrota_front.Platforms.Android.Services;

internal static class NotificacoesAndroid
{
    // Mesmo id que a API manda no push (NotificacaoApp::CANAL_ANDROID).
    public const string CanalId = "checkfrota_alertas";

    // Chaves do bloco "data" do push; o Android as repassa como extras ao abrir o MainActivity.
    public const string ExtraId = "notificacao_id";
    public const string ExtraDestino = "destino";

    // Precisa existir antes do primeiro push (Android 8+), senão a notificação é descartada.
    public static void CriarCanal(Context context)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
            return;

        var canal = new NotificationChannel(CanalId, "Alertas do CheckFrota", NotificationImportance.High)
        {
            Description = "Contas a vencer, viagens, lançamentos e aprovações de acesso",
        };

        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        manager?.CreateNotificationChannel(canal);
    }

    // Com o app aberto o Android não exibe o push sozinho; mostra igual ao que chega com ele fechado.
    public static void Exibir(Context context, string? titulo, string? mensagem, IDictionary<string, string> dados)
    {
        var intent = new Intent(context, typeof(MainActivity));
        intent.AddFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        foreach (var (chave, valor) in dados)
            intent.PutExtra(chave, valor);

        var codigo = Java.Lang.JavaSystem.CurrentTimeMillis().GetHashCode();
        var pendingIntent = PendingIntent.GetActivity(context, codigo, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var builder = new NotificationCompat.Builder(context, CanalId);
        builder.SetSmallIcon(Resource.Drawable.ic_notificacao);
        builder.SetContentTitle(titulo);
        builder.SetContentText(mensagem);
        builder.SetStyle(new NotificationCompat.BigTextStyle().BigText(mensagem));
        builder.SetPriority(NotificationCompat.PriorityHigh);
        builder.SetAutoCancel(true);
        builder.SetContentIntent(pendingIntent);

        try
        {
            NotificationManagerCompat.From(context)?.Notify(codigo, builder.Build());
        }
        catch (Java.Lang.SecurityException)
        {
            // Usuário negou a permissão de notificações; o aviso continua no sino.
        }
    }
}
