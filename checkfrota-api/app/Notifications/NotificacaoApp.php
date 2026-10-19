<?php

namespace App\Notifications;

use App\Models\User;
use App\Notifications\Channels\FcmChannel;
use Illuminate\Bus\Queueable;
use Illuminate\Contracts\Queue\ShouldQueue;
use Illuminate\Notifications\Notification;

/**
 * Notificação do app: fica no histórico (canal "database", listado em GET /notificacoes) e chega
 * como push no celular, mesmo com o app fechado. Vai pela fila para o envio ao Firebase não
 * atrasar a resposta da requisição.
 */
abstract class NotificacaoApp extends Notification implements ShouldQueue
{
    use Queueable;

    /**
     * Canal do Android em que o push aparece; criado pelo app com o mesmo id.
     */
    public const CANAL_ANDROID = 'checkfrota_alertas';

    abstract public function titulo(): string;

    abstract public function mensagem(): string;

    /**
     * Tela que o app abre ao tocar na notificação ("movimentacoes", "viagens", "usuarios"), ou
     * null para abrir só a lista de notificações.
     */
    public function destino(): ?string
    {
        return null;
    }

    /**
     * @return list<string|class-string>
     */
    public function via(User $notifiable): array
    {
        return ['database', FcmChannel::class];
    }

    /**
     * @return array{titulo: string, mensagem: string, destino: ?string}
     */
    public function toArray(User $notifiable): array
    {
        return [
            'titulo' => $this->titulo(),
            'mensagem' => $this->mensagem(),
            'destino' => $this->destino(),
        ];
    }

    /**
     * Campos de "message" da API v1 do Firebase. O bloco "data" volta para o app quando o usuário
     * toca na notificação, para marcar como lida e abrir a tela certa.
     *
     * @return array<string, mixed>
     */
    public function toFcm(User $notifiable): array
    {
        return [
            'notification' => [
                'title' => $this->titulo(),
                'body' => $this->mensagem(),
            ],
            'data' => [
                'notificacao_id' => (string) $this->id,
                'destino' => (string) $this->destino(),
            ],
            'android' => [
                'priority' => 'high',
                'notification' => ['channel_id' => self::CANAL_ANDROID],
            ],
        ];
    }

    protected function formatarValor(float|string $valor): string
    {
        return 'R$ '.number_format((float) $valor, 2, ',', '.');
    }
}
