namespace checkfrota_front.Services;

/// <summary>
/// Acesso ao push da plataforma (Firebase Cloud Messaging no Android).
/// </summary>
public interface IPushNotificationService
{
    /// <summary>Valor enviado à API para identificar o tipo de aparelho.</summary>
    string Plataforma { get; }

    /// <summary>
    /// Pede a permissão de notificações e devolve o token do aparelho, ou null quando a
    /// plataforma não tem push (Windows) ou o Firebase não está configurado.
    /// </summary>
    Task<string?> ObterTokenAsync();
}
