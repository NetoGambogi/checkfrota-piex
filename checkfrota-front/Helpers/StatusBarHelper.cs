#if ANDROID
using AndroidX.Core.View;
using Microsoft.Maui.Platform;
#endif

namespace checkfrota_front.Helpers;

// A área da barra de status do Android mostra o fundo da página, então a cor dos ícones
// (relógio, bateria...) precisa acompanhar esse fundo: ícones escuros em fundo claro e vice-versa.
public static class StatusBarHelper
{
    public static void Update(Page? page)
    {
#if ANDROID
        var window = Platform.CurrentActivity?.Window;
        if (window is null)
            return;

        var background = page?.BackgroundColor
            ?? (Application.Current?.Resources.TryGetValue("PageBg", out var value) == true ? value as Color : null)
            ?? Colors.White;

        // Antes do Android 15 a barra tem cor própria; a partir dele o app desenha por trás dela (edge-to-edge).
        if (!OperatingSystem.IsAndroidVersionAtLeast(35))
        {
#pragma warning disable CA1422
            window.SetStatusBarColor(background.ToPlatform());
#pragma warning restore CA1422
        }

        var controller = WindowCompat.GetInsetsController(window, window.DecorView);
        controller.AppearanceLightStatusBars = IsLight(background);
#endif
    }

    private static bool IsLight(Color color) =>
        0.299 * color.Red + 0.587 * color.Green + 0.114 * color.Blue > 0.5;
}
