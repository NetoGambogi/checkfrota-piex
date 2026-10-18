using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using checkfrota_front.Platforms.Android.Services;
using checkfrota_front.Services;

namespace checkfrota_front
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        // App fechado: o toque no push abre a Activity do zero.
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            ReceberToqueEmNotificacao(Intent);
        }

        // App em segundo plano: a Activity já existe e recebe o novo Intent.
        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);
            ReceberToqueEmNotificacao(intent);
        }

        private static void ReceberToqueEmNotificacao(Intent? intent)
        {
            var id = intent?.GetStringExtra(NotificacoesAndroid.ExtraId);
            if (intent is null || string.IsNullOrEmpty(id))
                return;

            var destino = intent.GetStringExtra(NotificacoesAndroid.ExtraDestino);

            // Evita reabrir a mesma notificação quando a Activity é recriada (ex.: girar a tela).
            intent.RemoveExtra(NotificacoesAndroid.ExtraId);

            IPlatformApplication.Current?.Services.GetService<NotificacaoService>()
                ?.ReceberToque(id, string.IsNullOrEmpty(destino) ? null : destino);
        }
    }
}
