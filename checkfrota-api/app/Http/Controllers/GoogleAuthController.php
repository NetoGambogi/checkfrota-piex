<?php

namespace App\Http\Controllers;

use Illuminate\Http\Request;
use App\Http\Controllers\Controller;
use App\Models\User;
use Illuminate\Support\Facades\Log;
use Laravel\Socialite\Facades\Socialite;

class GoogleAuthController extends Controller
{
    /**
     * Redireciona o usuário para a tela de login do Google.
     * GET /auth/google
     */
    public function redirect()
    {
        return Socialite::driver('google')->redirect();
    }

    /**
     * Recebe o retorno do Google, cria/atualiza o usuário local
     * e emite um token de API (Sanctum) para o cliente (MAUI).
     * GET /auth/google/callback
     */
    public function callback()
    {
        try {
            $googleUser = Socialite::driver('google')->user();
        } catch (\Throwable $e) {
            Log::error('Falha na autenticação com o Google: ' . $e->getMessage());

            return response()->json([
                'message' => 'Não foi possível autenticar com o Google.',
            ], 401);
        }

        // Busca o usuário pelo google_id ou pelo e-mail (caso já exista
        // um cadastro local feito antes de o usuário usar o login Google).
        $user = User::where('google_id', $googleUser->getId())
            ->orWhere('email', $googleUser->getEmail())
            ->first();

        if ($user) {
            // Garante que o google_id fique salvo, caso o usuário já
            // existisse antes só com e-mail/senha.
            $user->update([
                'google_id' => $googleUser->getId(),
                'avatar'    => $googleUser->getAvatar(),
            ]);
        } else {
            $user = User::create([
                'name'      => $googleUser->getName(),
                'email'     => $googleUser->getEmail(),
                'google_id' => $googleUser->getId(),
                'avatar'    => $googleUser->getAvatar(),
                // Senha aleatória: o usuário nunca vai logar com ela,
                // mas a coluna costuma ser NOT NULL no schema padrão do Laravel.
                'password'  => bcrypt(str()->random(24)),
                // Ajuste conforme o RBAC do projeto. Todo novo usuário
                // entra como "motorista" por padrão; promoção a admin
                // deve ser feita manualmente ou por outro admin.
                'role'      => 'motorista',
            ]);
        }

        // Remove tokens antigos com o mesmo nome antes de emitir um novo,
        // evitando acumular tokens "maui-app" a cada novo login.
        $user->tokens()->where('name', 'maui-app')->delete();

        $token = $user->createToken('maui-app')->plainTextToken;

        return response()->json([
            'token' => $token,
            'user'  => [
                'id'    => $user->id,
                'name'  => $user->name,
                'email' => $user->email,
                'role'  => $user->role,
            ],
        ]);
    }
}