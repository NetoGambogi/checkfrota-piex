using checkfrota_front.Services;

namespace checkfrota_front.Platforms.Windows.Services;

// No Windows não há push: as notificações aparecem só no sino, ao abrir o app.
public class PushNotificationService : IPushNotificationService
{
    public string Plataforma => "windows";

    public Task<string?> ObterTokenAsync() => Task.FromResult<string?>(null);
}
