<?php

namespace App\Http\Middleware;

use Closure;
use Illuminate\Http\Request;
use Laravel\Sanctum\PersonalAccessToken;
use Symfony\Component\HttpFoundation\Response;

/**
 * Sessão expira por inatividade: cada requisição autenticada empurra a validade do token para
 * DIAS_INATIVIDADE dias à frente. Quem fica esse tempo sem usar o app precisa entrar de novo.
 */
class RenovarExpiracaoToken
{
    public const DIAS_INATIVIDADE = 7;

    /**
     * @param  Closure(Request): (Response)  $next
     */
    public function handle(Request $request, Closure $next): Response
    {
        $token = $request->user()?->currentAccessToken();

        if ($token instanceof PersonalAccessToken) {
            $novaExpiracao = now()->addDays(self::DIAS_INATIVIDADE);

            // Grava no máximo uma vez por hora, para não escrever no banco a cada requisição.
            // Tokens antigos (sem validade) passam a expirar a partir do próximo uso.
            if ($token->expires_at === null || $token->expires_at->lt($novaExpiracao->copy()->subHour())) {
                $token->forceFill(['expires_at' => $novaExpiracao])->save();
            }
        }

        return $next($request);
    }
}
