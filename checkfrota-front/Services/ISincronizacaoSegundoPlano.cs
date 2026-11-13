namespace checkfrota_front.Services;

/// <summary>
/// Pede ao sistema para enviar a <see cref="FilaOffline"/> quando houver internet, mesmo com o app
/// fechado. Só existe no Android (WorkManager); nas outras plataformas a fila é enviada com o app aberto.
/// </summary>
public interface ISincronizacaoSegundoPlano
{
    /// <summary>Agenda o envio para quando houver rede; se já há um agendado, mantém o existente.</summary>
    void Agendar();
}
