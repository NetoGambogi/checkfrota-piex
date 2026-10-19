<?php

namespace App\Http\Controllers;

use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Illuminate\Notifications\DatabaseNotification;

class NotificacaoController extends Controller
{
    private const LIMITE = 50;

    /**
     * Últimas notificações do usuário logado e a quantidade de não lidas (contador do sino).
     */
    public function index(Request $request): JsonResponse
    {
        $user = $request->user();

        $notificacoes = $user->notifications()->limit(self::LIMITE)->get();

        return response()->json([
            'nao_lidas' => $user->unreadNotifications()->count(),
            'notificacoes' => $notificacoes->map(fn (DatabaseNotification $notificacao) => [
                'id' => $notificacao->id,
                'titulo' => $notificacao->data['titulo'] ?? '',
                'mensagem' => $notificacao->data['mensagem'] ?? '',
                'destino' => $notificacao->data['destino'] ?? null,
                'lida' => $notificacao->read_at !== null,
                'criada_em' => $notificacao->created_at?->toIso8601String(),
            ]),
        ]);
    }

    public function marcarComoLida(Request $request, string $notificacao): JsonResponse
    {
        $request->user()->notifications()->findOrFail($notificacao)->markAsRead();

        return response()->json(['nao_lidas' => $request->user()->unreadNotifications()->count()]);
    }

    public function marcarTodasComoLidas(Request $request): JsonResponse
    {
        $request->user()->unreadNotifications()->update(['read_at' => now()]);

        return response()->json(['nao_lidas' => 0]);
    }
}
