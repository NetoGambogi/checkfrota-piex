<?php

namespace App\Notifications\Channels;

use App\Models\User;
use App\Notifications\NotificacaoApp;
use App\Services\FirebaseMessagingService;

/**
 * Entrega a notificação como push (Firebase Cloud Messaging) em todos os aparelhos do usuário.
 */
class FcmChannel
{
    public function __construct(private FirebaseMessagingService $firebase) {}

    public function send(User $notifiable, NotificacaoApp $notification): void
    {
        if (! $this->firebase->configurado()) {
            return;
        }

        $mensagem = $notification->toFcm($notifiable);

        foreach ($notifiable->dispositivos as $dispositivo) {
            if (! $this->firebase->enviar($dispositivo->token, $mensagem)) {
                $dispositivo->delete();
            }
        }
    }
}
